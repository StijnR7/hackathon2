using HitchTracker.Web.Data;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Routing;
using HitchTracker.Web.Services.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services.Simulation;

/// <summary>
/// Speelt voor demo-doeleinden het taxikastje na: de taxi rijdt over de route (of bewust om)
/// en geeft GPS-posities door aan <see cref="TripService"/>, precies zoals een echte taxi dat via de API zou doen.
/// </summary>
public class TripSimulator(
    HitchTrackerDbContext db,
    IRoutingService routing,
    TripService trips,
    IOptions<SimulationOptions> options,
    ILogger<TripSimulator> logger)
{
    private const double DefaultSpeedMetersPerSecond = 25 / 3.6;

    /// <summary>Maakt een simulatie voor een gestarte rit.</summary>
    /// <param name="withDetour">Laat de chauffeur omrijden, om de waarschuwingen te demonstreren.</param>
    public async Task CreateAsync(Trip trip, bool withDetour, CancellationToken cancellationToken = default)
    {
        var path = trip.PlannedRoute.ToList();
        if (withDetour)
        {
            // Beide kanten van de route proberen en de langste omweg kiezen: afhankelijk van het stratenplan
            // kan een punt aan de ene kant toevallig "op weg" liggen.
            var left = await routing.GetRouteAsync([trip.Pickup, DetourPointFor(trip, side: 1), trip.Destination], cancellationToken);
            var right = await routing.GetRouteAsync([trip.Pickup, DetourPointFor(trip, side: -1), trip.Destination], cancellationToken);
            path = GeoMath.PathLength(left.Points) >= GeoMath.PathLength(right.Points) ? left.Points : right.Points;
        }

        var speed = trip.PlannedDurationSeconds > 0
            ? trip.PlannedDistanceMeters / trip.PlannedDurationSeconds
            : DefaultSpeedMetersPerSecond;

        db.TripSimulations.Add(new TripSimulation
        {
            TripId = trip.Id,
            Path = path,
            PathLengthMeters = GeoMath.PathLength(path),
            SpeedMetersPerSecond = Math.Clamp(speed, 2, 30),
            WithDetour = withDetour
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Een punt naast de route: halverwege, loodrecht op de rijrichting, ca. 40% van de ritafstand opzij.
    /// </summary>
    /// <param name="side">1 = links van de rijrichting, -1 = rechts.</param>
    public static GeoPoint DetourPointFor(Trip trip, int side = 1)
    {
        var middle = GeoMath.PointAlongPath(trip.PlannedRoute, trip.PlannedDistanceMeters / 2);
        var straightDistance = GeoMath.Distance(trip.Pickup, trip.Destination);

        // Rijrichting in meters (noord, oost) en de loodrechte richting daarop.
        var north = GeoMath.Distance(trip.Pickup, new GeoPoint(trip.DestinationLat, trip.PickupLng)) *
                    Math.Sign(trip.DestinationLat - trip.PickupLat);
        var east = GeoMath.Distance(trip.Pickup, new GeoPoint(trip.PickupLat, trip.DestinationLng)) *
                   Math.Sign(trip.DestinationLng - trip.PickupLng);
        var length = Math.Max(1, Math.Sqrt(north * north + east * east));
        var offset = Math.Max(800, straightDistance * 0.4) * Math.Sign(side);

        return GeoMath.Offset(middle, -east / length * offset, north / length * offset);
    }

    /// <summary>Laat alle gesimuleerde taxi's die onderweg zijn een stukje verder rijden.</summary>
    public async Task<int> StepAllAsync(TimeSpan realElapsed, CancellationToken cancellationToken = default)
    {
        var activeTripIds = await db.TripSimulations.AsNoTracking()
            .Where(s => db.Trips.Any(t => t.Id == s.TripId && t.Status == TripStatus.InProgress))
            .Select(s => s.TripId)
            .ToListAsync(cancellationToken);

        foreach (var tripId in activeTripIds)
        {
            try
            {
                await StepAsync(tripId, realElapsed, cancellationToken);
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or TripStateException)
            {
                // De reiziger heeft de rit net beëindigd of tegelijk iets gewijzigd: volgende tick opnieuw.
                logger.LogDebug(ex, "Simulatiestap voor rit {TripId} overgeslagen.", tripId);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return activeTripIds.Count;
    }

    /// <summary>Rijdt één taxi verder en geeft de nieuwe posities door.</summary>
    public async Task StepAsync(Guid tripId, TimeSpan realElapsed, CancellationToken cancellationToken = default)
    {
        var simulation = await db.TripSimulations.SingleAsync(s => s.TripId == tripId, cancellationToken);
        var startedAt = await db.Trips.Where(t => t.Id == tripId).Select(t => t.StartedAt).SingleAsync(cancellationToken)
                        ?? throw new TripStateException("De rit is nog niet gestart.");

        var from = simulation.DistanceTravelledMeters;
        var to = Math.Min(simulation.PathLengthMeters,
            from + simulation.SpeedMetersPerSecond * realElapsed.TotalSeconds * options.Value.SpeedFactor);

        if (to > from)
        {
            // Alle hoekpunten van het verreden stuk doorgeven, zodat de gereden route de weg exact volgt.
            // De tijdstempel volgt de gesimuleerde klok: afstand gedeeld door snelheid.
            var points = GeoMath.SubPath(simulation.Path, from, to);
            var reports = new List<PositionReport>();
            var distanceAlong = from;
            for (var i = 1; i < points.Count; i++)
            {
                distanceAlong = Math.Min(to, distanceAlong + GeoMath.Distance(points[i - 1], points[i]));
                reports.Add(new PositionReport(points[i],
                    startedAt.AddSeconds(distanceAlong / simulation.SpeedMetersPerSecond)));
            }

            simulation.DistanceTravelledMeters = to;
            // RecordPositionsAsync slaat de voortgang van de simulatie in dezelfde transactie op.
            await trips.RecordPositionsAsync(tripId, reports, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (simulation.DistanceTravelledMeters >= simulation.PathLengthMeters)
        {
            await trips.CompleteTripAsync(tripId, cancellationToken);
        }
    }
}

using HitchTracker.Web.Models;
using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services.Trips;

/// <summary>
/// Bepaalt of de taxi zich nog aan de geplande route houdt. Bevat geen database- of netwerkcode,
/// zodat de regels los te testen zijn.
/// </summary>
public class TripMonitor(IOptions<TripMonitoringOptions> options)
{
    /// <summary>Hoeveel meter terug over de route we nog accepteren (bijv. na een rondje om het blok).</summary>
    private const double BacktrackToleranceMeters = 200;

    public RouteCheck CheckPosition(Trip trip, GeoPoint position)
    {
        if (trip.PlannedRoute.Count == 0)
        {
            throw new InvalidOperationException("De rit heeft geen geplande route.");
        }

        var settings = options.Value;
        var minimumProgress = Math.Max(0, trip.ProgressAlongRouteMeters - BacktrackToleranceMeters);
        var projection = GeoMath.ProjectOnPath(position, trip.PlannedRoute, minimumProgress);

        // Hysterese: wie al van de route af is, moet duidelijk terug zijn om weer "op route" te gelden.
        var threshold = trip.IsOffRoute ? settings.BackOnRouteThresholdMeters : settings.OffRouteThresholdMeters;
        var isOffRoute = projection.DistanceFromPath > threshold;

        return new RouteCheck(
            DistanceFromRouteMeters: projection.DistanceFromPath,
            DistanceAlongRouteMeters: projection.DistanceAlongPath,
            IsOffRoute: isOffRoute,
            LeftRoute: isOffRoute && !trip.IsOffRoute,
            ReturnedToRoute: !isOffRoute && trip.IsOffRoute);
    }
}

public readonly record struct RouteCheck(
    double DistanceFromRouteMeters,
    double DistanceAlongRouteMeters,
    bool IsOffRoute,
    bool LeftRoute,
    bool ReturnedToRoute);

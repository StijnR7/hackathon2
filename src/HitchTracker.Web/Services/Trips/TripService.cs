using System.Globalization;
using HitchTracker.Web.Data;
using HitchTracker.Web.Infrastructure;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services.Trips;

/// <summary>Een locatie zoals de reiziger die opgeeft: een bekende plaats óf een punt op de kaart.</summary>
public sealed record LocationInput(int? PlaceId, double? Lat, double? Lng);

/// <summary>Een GPS-meting van het taxikastje (of de simulator).</summary>
public sealed record PositionReport(GeoPoint Position, DateTime RecordedAtUtc);

/// <summary>
/// Alle bedrijfslogica rond een rit: plannen en schatten (epic 1), live volgen met meter en waarschuwingen (epic 2)
/// en afrekenen met eindprijs en beoordeling (epic 3).
/// </summary>
public class TripService(
    HitchTrackerDbContext db,
    IRoutingService routing,
    TripMonitor monitor,
    TimeProvider clock,
    IOptions<TripMonitoringOptions> options,
    ILogger<TripService> logger)
{
    public const int MaxMessageLength = 200;
    public const int MaxCommentLength = 500;
    private const int MaxConcurrencyRetries = 3;

    /// <summary>Standaardberichten, zodat de reiziger de chauffeur ook zonder gemeenschappelijke taal kan aanspreken.</summary>
    public static readonly IReadOnlyList<string> QuickMessages =
    [
        "Wilt u alstublieft de route in de app volgen?",
        "Waarom rijdt u een andere route dan gepland?",
        "Kunt u iets rustiger rijden, alstublieft?",
        "Ik wil graag hier uitstappen."
    ];

    private TripMonitoringOptions Settings => options.Value;
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------
    // Epic 1: voor de reis
    // ------------------------------------------------------------------

    /// <summary>Berekent route en prijsschatting en slaat de rit op als "gepland".</summary>
    public async Task<Trip> PlanTripAsync(int cityId, LocationInput pickupInput, LocationInput destinationInput,
        CancellationToken cancellationToken = default)
    {
        var city = await db.Cities.Include(c => c.Tariffs).SingleOrDefaultAsync(c => c.Id == cityId, cancellationToken)
                   ?? throw new TripValidationException("Kies een geldige stad.");
        var pickup = await ResolveLocationAsync(city, pickupInput, "ophaallocatie", cancellationToken);
        var destination = await ResolveLocationAsync(city, destinationInput, "bestemming", cancellationToken);

        if (GeoMath.Distance(pickup.Point, destination.Point) < Settings.MinimumTripDistanceMeters)
        {
            throw new TripValidationException("De ophaallocatie en de bestemming liggen te dicht bij elkaar voor een taxirit.");
        }

        var now = UtcNow;
        var tariff = TariffSelector.Select(city, now)
                     ?? throw new TripValidationException($"Er is geen taxitarief bekend voor {city.Name} op dit tijdstip.");

        RouteResult route;
        try
        {
            route = await routing.GetRouteAsync([pickup.Point, destination.Point], cancellationToken);
        }
        catch (RoutingException ex)
        {
            logger.LogError(ex, "Geen route gevonden in {City}.", city.Name);
            throw new TripValidationException("Er kon geen route worden berekend. Probeer het later opnieuw.");
        }

        // De meter meet de afstand over de polyline. Door de schatting op dezelfde maat te baseren,
        // betaalt de reiziger bij het rijden van de geplande route precies de geschatte prijs.
        var distance = GeoMath.PathLength(route.Points);
        var rates = tariff.Rates.Copy();

        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Status = TripStatus.Planned,
            PickupName = pickup.Name,
            PickupLat = pickup.Point.Lat,
            PickupLng = pickup.Point.Lng,
            DestinationName = destination.Name,
            DestinationLat = destination.Point.Lat,
            DestinationLng = destination.Point.Lng,
            PlannedRoute = route.Points,
            PlannedDistanceMeters = distance,
            PlannedDurationSeconds = route.DurationSeconds,
            RouteSource = route.Source,
            TariffName = tariff.Name,
            Rates = rates,
            CurrencyCode = city.CurrencyCode,
            EstimatedPrice = rates.Calculate(distance, route.DurationSeconds).Total,
            EstimatedPriceHigh = rates.Calculate(distance, route.DurationSeconds * Settings.TrafficMarginFactor).Total,
            RemainingRoute = route.Points.ToList(),
            RemainingDistanceMeters = distance,
            RemainingDurationSeconds = route.DurationSeconds,
            CreatedAt = now
        };
        trip.EstimatedFinalPrice = trip.EstimatedPrice;

        db.Trips.Add(trip);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rit {TripId} gepland in {City} ({Distance:0} m, route via {Source}).",
            trip.Id, city.Name, distance, route.Source);
        return trip;
    }

    /// <summary>De reiziger stapt in de gekozen taxi: de meter gaat lopen.</summary>
    public async Task<Trip> StartTripAsync(Guid tripId, int taxiId, CancellationToken cancellationToken = default)
    {
        var trip = await LoadTripAsync(tripId, cancellationToken);
        if (trip.Status != TripStatus.Planned)
        {
            throw new TripStateException("Deze rit is al gestart of afgerond.");
        }

        var taxi = await db.Taxis.Include(t => t.Driver)
                       .SingleOrDefaultAsync(t => t.Id == taxiId && t.CityId == trip.CityId, cancellationToken)
                   ?? throw new TripValidationException("Kies een taxi uit de lijst.");
        if (!taxi.IsAvailable)
        {
            throw new TripValidationException($"Taxi {taxi.LicensePlate} is op dit moment niet beschikbaar.");
        }

        // De taxi staat op de weg: het beginpunt van de route (de routeplanner "snapt" de ophaallocatie naar de
        // dichtstbijzijnde weg). Zo telt de meter geen meters tussen gebouw en straat mee.
        var start = trip.PlannedRoute.Count > 0 ? trip.PlannedRoute[0] : trip.Pickup;
        var now = UtcNow;
        taxi.IsAvailable = false;
        trip.Taxi = taxi;
        trip.Status = TripStatus.InProgress;
        trip.StartedAt = now;
        trip.CurrentLat = start.Lat;
        trip.CurrentLng = start.Lng;
        trip.LastPositionAt = now;
        trip.CurrentPrice = trip.Rates.Calculate(0, 0).Total;
        trip.Positions.Add(new TripPosition { Lat = start.Lat, Lng = start.Lng, RecordedAt = now });
        trip.Version++;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rit {TripId} gestart met taxi {TaxiId}.", trip.Id, taxi.Id);
        return trip;
    }

    // ------------------------------------------------------------------
    // Epic 2: tijdens de reis
    // ------------------------------------------------------------------

    /// <summary>
    /// Verwerkt GPS-metingen van de taxi: meter bijwerken, afwijking van de route bewaken,
    /// resterende route en verwachte eindprijs herberekenen.
    /// </summary>
    /// <returns>Het aantal geaccepteerde metingen (onrealistische of verouderde metingen worden genegeerd).</returns>
    public async Task<int> RecordPositionsAsync(Guid tripId, IReadOnlyList<PositionReport> reports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reports);
        var trip = await LoadTripAsync(tripId, cancellationToken);
        if (trip.Status != TripStatus.InProgress)
        {
            throw new TripStateException("Deze rit is niet (meer) onderweg.");
        }

        var accepted = 0;
        foreach (var report in reports.OrderBy(r => r.RecordedAtUtc))
        {
            if (TryApplyPosition(trip, report))
            {
                accepted++;
            }
        }

        if (accepted == 0)
        {
            return 0;
        }

        await UpdateRemainingRouteAsync(trip, cancellationToken);
        UpdatePriceExpectation(trip);
        trip.Version++;

        await db.SaveChangesAsync(cancellationToken);
        return accepted;
    }

    /// <summary>Stuurt een bericht van de reiziger naar het scherm van de chauffeur.</summary>
    public async Task SendPassengerMessageAsync(Guid tripId, string? message, CancellationToken cancellationToken = default)
    {
        var text = message?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new TripValidationException("Typ een bericht of kies een standaardbericht.");
        }

        if (text.Length > MaxMessageLength)
        {
            throw new TripValidationException($"Een bericht mag maximaal {MaxMessageLength} tekens lang zijn.");
        }

        var trip = await LoadTripAsync(tripId, cancellationToken);
        if (trip.Status != TripStatus.InProgress)
        {
            throw new TripStateException("Je kunt alleen tijdens de rit een bericht sturen.");
        }

        db.TripAlerts.Add(new TripAlert
        {
            TripId = trip.Id,
            Type = TripAlertType.PassengerMessage,
            Message = text,
            CreatedAt = UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------
    // Epic 3: na de reis
    // ------------------------------------------------------------------

    /// <summary>De taxi is op de bestemming: de meterstand wordt de eindprijs.</summary>
    public Task<Trip> CompleteTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        FinishTripAsync(tripId, endedEarly: false, cancellationToken);

    /// <summary>De reiziger stopt de rit voortijdig (bijv. omdat de chauffeur blijft omrijden).</summary>
    public Task<Trip> EndTripEarlyAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        FinishTripAsync(tripId, endedEarly: true, cancellationToken);

    public async Task SubmitFeedbackAsync(Guid tripId, int stars, string? comment, bool reportOvercharge,
        CancellationToken cancellationToken = default)
    {
        if (stars is < 1 or > 5)
        {
            throw new TripValidationException("Geef een beoordeling van 1 tot en met 5 sterren.");
        }

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text?.Length > MaxCommentLength)
        {
            throw new TripValidationException($"Een toelichting mag maximaal {MaxCommentLength} tekens lang zijn.");
        }

        var trip = await db.Trips.Include(t => t.Feedback).SingleOrDefaultAsync(t => t.Id == tripId, cancellationToken)
                   ?? throw new TripNotFoundException(tripId);
        if (!trip.IsFinished)
        {
            throw new TripStateException("Je kunt een rit pas na afloop beoordelen.");
        }

        if (trip.Feedback is not null)
        {
            throw new TripStateException("Je hebt deze rit al beoordeeld.");
        }

        trip.Feedback = new TripFeedback
        {
            Stars = stars,
            Comment = text,
            ReportedOvercharge = reportOvercharge,
            CreatedAt = UtcNow
        };
        await db.SaveChangesAsync(cancellationToken);

        if (reportOvercharge)
        {
            logger.LogWarning("Melding te hoge ritprijs voor rit {TripId} (taxi {TaxiId}).", trip.Id, trip.TaxiId);
        }
    }

    // ------------------------------------------------------------------
    // Opvragen
    // ------------------------------------------------------------------

    /// <summary>Rit met stad, taxi, chauffeur, meldingen en beoordeling (alleen-lezen).</summary>
    public async Task<Trip> GetTripDetailsAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        await db.Trips.AsNoTracking()
            .Include(t => t.City)
            .Include(t => t.Taxi).ThenInclude(t => t!.Driver)
            .Include(t => t.Alerts)
            .Include(t => t.Feedback)
            .AsSplitQuery()
            .SingleOrDefaultAsync(t => t.Id == tripId, cancellationToken)
        ?? throw new TripNotFoundException(tripId);

    /// <summary>De gereden route: alle GPS-posities op volgorde.</summary>
    public async Task<List<GeoPoint>> GetDrivenRouteAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        await db.TripPositions.AsNoTracking()
            .Where(p => p.TripId == tripId)
            .OrderBy(p => p.RecordedAt).ThenBy(p => p.Id)
            .Select(p => new GeoPoint(p.Lat, p.Lng))
            .ToListAsync(cancellationToken);

    /// <summary>Actuele stand van de rit voor het live scherm.</summary>
    public async Task<TripStatusDto> GetStatusAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.AsNoTracking()
                       .Include(t => t.Alerts)
                       .SingleOrDefaultAsync(t => t.Id == tripId, cancellationToken)
                   ?? throw new TripNotFoundException(tripId);
        var driven = await GetDrivenRouteAsync(tripId, cancellationToken);
        return TripStatusDto.From(trip, driven);
    }

    // ------------------------------------------------------------------
    // Hulpmethodes
    // ------------------------------------------------------------------

    private async Task<Trip> FinishTripAsync(Guid tripId, bool endedEarly, CancellationToken cancellationToken)
    {
        // De simulator kan tegelijk posities wegschrijven; bij een conflict lezen we de rit opnieuw in.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryFinishTripAsync(tripId, endedEarly, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Trip> TryFinishTripAsync(Guid tripId, bool endedEarly, CancellationToken cancellationToken)
    {
        var trip = await db.Trips.Include(t => t.Taxi).SingleOrDefaultAsync(t => t.Id == tripId, cancellationToken)
                   ?? throw new TripNotFoundException(tripId);
        if (trip.Status != TripStatus.InProgress)
        {
            throw new TripStateException(trip.IsFinished ? "Deze rit is al afgelopen." : "Deze rit is nog niet gestart.");
        }

        trip.Status = endedEarly ? TripStatus.EndedEarly : TripStatus.Completed;
        trip.EndedAt = UtcNow;
        trip.FinalPrice = trip.CurrentPrice;
        trip.EstimatedFinalPrice = trip.CurrentPrice;
        trip.RemainingRoute = [];
        trip.RemainingDistanceMeters = 0;
        trip.RemainingDurationSeconds = 0;
        if (trip.Taxi is not null)
        {
            trip.Taxi.IsAvailable = true;
        }

        if (endedEarly)
        {
            AddAlert(trip, TripAlertType.TripEndedEarly,
                $"Je hebt de rit voortijdig beëindigd. Te betalen volgens de meter: {Formatting.Money(trip.CurrentPrice, trip.CurrencyCode)}.");
        }

        trip.Version++;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rit {TripId} beëindigd ({Status}).", trip.Id, trip.Status);
        return trip;
    }

    private bool TryApplyPosition(Trip trip, PositionReport report)
    {
        if (!report.Position.IsValid)
        {
            return false;
        }

        var recordedAt = DateTime.SpecifyKind(report.RecordedAtUtc, DateTimeKind.Utc);
        var previousPosition = trip.CurrentPosition ?? trip.Pickup;
        var previousTime = trip.LastPositionAt ?? trip.StartedAt ?? recordedAt;
        if (recordedAt < previousTime)
        {
            return false; // verouderde meting
        }

        var step = GeoMath.Distance(previousPosition, report.Position);
        var seconds = (recordedAt - previousTime).TotalSeconds;
        var maxMetersPerSecond = Settings.MaxPlausibleSpeedKmh / 3.6;
        var implausible = seconds > 0 ? step / seconds > maxMetersPerSecond : step > 5;
        if (implausible)
        {
            logger.LogWarning("Onrealistische GPS-sprong van {Step:0} m genegeerd voor rit {TripId}.", step, trip.Id);
            return false;
        }

        var check = monitor.CheckPosition(trip, report.Position);

        trip.DrivenDistanceMeters += step;
        trip.ElapsedSeconds = (recordedAt - (trip.StartedAt ?? recordedAt)).TotalSeconds;
        trip.CurrentLat = report.Position.Lat;
        trip.CurrentLng = report.Position.Lng;
        trip.LastPositionAt = recordedAt;
        trip.CurrentPrice = trip.Rates.Calculate(trip.DrivenDistanceMeters, trip.ElapsedSeconds).Total;
        trip.IsOffRoute = check.IsOffRoute;
        if (!check.IsOffRoute)
        {
            trip.ProgressAlongRouteMeters = Math.Max(trip.ProgressAlongRouteMeters, check.DistanceAlongRouteMeters);
        }

        if (check.LeftRoute)
        {
            AddAlert(trip, TripAlertType.RouteDeviation,
                $"Let op: de taxi wijkt {check.DistanceFromRouteMeters:0} meter af van de geplande route. " +
                "Vraag de chauffeur waarom, of beëindig de rit.");
        }
        else if (check.ReturnedToRoute)
        {
            AddAlert(trip, TripAlertType.BackOnRoute, "De taxi rijdt weer over de geplande route.");
        }

        trip.Positions.Add(new TripPosition
        {
            Lat = report.Position.Lat,
            Lng = report.Position.Lng,
            RecordedAt = recordedAt,
            DistanceFromRouteMeters = check.DistanceFromRouteMeters
        });
        return true;
    }

    private async Task UpdateRemainingRouteAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (!trip.IsOffRoute)
        {
            // Op route: de rest van de geplande route.
            trip.RemainingRoute = GeoMath.RemainingPath(trip.PlannedRoute, trip.ProgressAlongRouteMeters);
            trip.RemainingDistanceMeters = Math.Max(0, trip.PlannedDistanceMeters - trip.ProgressAlongRouteMeters);
            trip.RemainingDurationSeconds = trip.PlannedDistanceMeters > 0
                ? trip.PlannedDurationSeconds * trip.RemainingDistanceMeters / trip.PlannedDistanceMeters
                : 0;
            trip.RemainingRouteCalculatedAt = null;
            return;
        }

        var current = trip.CurrentPosition ?? trip.Pickup;
        var now = UtcNow;
        var recentlyRerouted = trip.RemainingRouteCalculatedAt is { } calculatedAt &&
                               (now - calculatedAt).TotalSeconds < Settings.RerouteIntervalSeconds;
        if (recentlyRerouted)
        {
            ShortenRemainingRoute(trip, current);
            return;
        }

        // Afgeweken: bereken de beste route vanaf de huidige positie naar de bestemming.
        try
        {
            var route = await routing.GetRouteAsync([current, trip.Destination], cancellationToken);
            trip.RemainingRoute = route.Points;
            trip.RemainingDistanceMeters = GeoMath.PathLength(route.Points);
            trip.RemainingDurationSeconds = route.DurationSeconds;
            trip.RemainingRouteCalculatedAt = now;
        }
        catch (RoutingException ex)
        {
            logger.LogWarning(ex, "Herberekenen van de route voor rit {TripId} mislukt.", trip.Id);
            ShortenRemainingRoute(trip, current);
        }
    }

    /// <summary>Tussen twee herberekeningen door: haal het verreden stuk van de laatst berekende route af.</summary>
    private void ShortenRemainingRoute(Trip trip, GeoPoint current)
    {
        if (trip.RemainingRoute.Count < 2 || trip.RemainingDistanceMeters <= 0)
        {
            return;
        }

        var projection = GeoMath.ProjectOnPath(current, trip.RemainingRoute);
        if (projection.DistanceFromPath > Settings.OffRouteThresholdMeters)
        {
            return; // taxi rijdt ook niet over de herberekende route; wacht op de volgende herberekening
        }

        var previousDistance = trip.RemainingDistanceMeters;
        trip.RemainingRoute = GeoMath.RemainingPath(trip.RemainingRoute, projection.DistanceAlongPath);
        trip.RemainingDistanceMeters = GeoMath.PathLength(trip.RemainingRoute);
        trip.RemainingDurationSeconds *= trip.RemainingDistanceMeters / previousDistance;
    }

    private void UpdatePriceExpectation(Trip trip)
    {
        trip.EstimatedFinalPrice = trip.Rates.Calculate(
            trip.DrivenDistanceMeters + trip.RemainingDistanceMeters,
            trip.ElapsedSeconds + trip.RemainingDurationSeconds).Total;

        if (trip.EstimatedFinalPrice > trip.EstimatedPriceHigh && !trip.PriceAlertSent)
        {
            trip.PriceAlertSent = true;
            AddAlert(trip, TripAlertType.PriceAboveEstimate,
                $"De verwachte eindprijs ({Formatting.Money(trip.EstimatedFinalPrice, trip.CurrencyCode)}) is hoger dan " +
                $"de bovengrens van de schatting ({Formatting.Money(trip.EstimatedPriceHigh, trip.CurrencyCode)}).");
        }
    }

    private void AddAlert(Trip trip, TripAlertType type, string message) =>
        trip.Alerts.Add(new TripAlert { Type = type, Message = message, CreatedAt = UtcNow });

    private async Task<Trip> LoadTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        await db.Trips.SingleOrDefaultAsync(t => t.Id == tripId, cancellationToken)
        ?? throw new TripNotFoundException(tripId);

    private async Task<NamedLocation> ResolveLocationAsync(City city, LocationInput input, string label,
        CancellationToken cancellationToken)
    {
        if (input.PlaceId is { } placeId)
        {
            var place = await db.Places.AsNoTracking()
                            .SingleOrDefaultAsync(p => p.Id == placeId && p.CityId == city.Id, cancellationToken)
                        ?? throw new TripValidationException($"De gekozen {label} bestaat niet in {city.Name}.");
            return new NamedLocation(place.Name, place.Location);
        }

        if (input.Lat is { } lat && input.Lng is { } lng)
        {
            var point = new GeoPoint(lat, lng);
            if (!point.IsValid)
            {
                throw new TripValidationException($"De {label} heeft ongeldige coördinaten.");
            }

            if (GeoMath.Distance(point, city.Center) > city.ServiceRadiusKm * 1000)
            {
                throw new TripValidationException($"De {label} ligt buiten het werkgebied van {city.Name}.");
            }

            var name = string.Create(CultureInfo.InvariantCulture, $"Punt op de kaart ({lat:0.0000}, {lng:0.0000})");
            return new NamedLocation(name, point);
        }

        throw new TripValidationException($"Kies een {label} uit de lijst of klik op de kaart.");
    }

    private sealed record NamedLocation(string Name, GeoPoint Point);
}

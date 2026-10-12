using HitchTracker.Tests.TestSupport;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Trips;
using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Tests.Unit;

/// <summary>Tests van de bedrijfslogica rond een rit, tegen een echte (in-memory) SQLite-database.</summary>
public sealed class TripServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // ---------------- Epic 1: voor de reis ----------------

    [Fact] // UT-24
    public async Task PlanTrip_CalculatesEstimateFromRouteAndTariff()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);

        var trip = await TripScenario.PlanAmsterdamTripAsync(_database, trips);

        var expected = trip.Rates.Calculate(trip.PlannedDistanceMeters, trip.PlannedDurationSeconds).Total;
        Assert.Equal(TripStatus.Planned, trip.Status);
        Assert.Equal(expected, trip.EstimatedPrice);
        Assert.True(trip.EstimatedPriceHigh > trip.EstimatedPrice);
        Assert.Equal("Maximumtarief", trip.TariffName);
        Assert.Equal("EUR", trip.CurrencyCode);
        Assert.True(trip.PlannedRoute.Count >= 2);
        Assert.Equal("Amsterdam Centraal", trip.PickupName);
    }

    [Fact] // UT-25
    public async Task PlanTrip_WithMapPoint_UsesCoordinates()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var city = _database.City("Amsterdam");

        var trip = await trips.PlanTripAsync(city.Id,
            new LocationInput(null, 52.3600, 4.8800),
            new LocationInput(_database.PlaceId("De Dam"), null, null));

        Assert.StartsWith("Punt op de kaart", trip.PickupName);
        Assert.Equal(52.36, trip.PickupLat);
    }

    [Fact] // UT-26
    public async Task PlanTrip_OutsideServiceArea_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var city = _database.City("Amsterdam");

        var error = await Assert.ThrowsAsync<TripValidationException>(() => trips.PlanTripAsync(city.Id,
            new LocationInput(null, 51.9225, 4.4792), // Rotterdam
            new LocationInput(_database.PlaceId("De Dam"), null, null)));
        Assert.Contains("buiten het werkgebied", error.Message);
    }

    [Fact] // UT-27
    public async Task PlanTrip_PlaceFromOtherCity_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var amsterdam = _database.City("Amsterdam");

        await Assert.ThrowsAsync<TripValidationException>(() => trips.PlanTripAsync(amsterdam.Id,
            new LocationInput(_database.PlaceId("Big Ben"), null, null),
            new LocationInput(_database.PlaceId("De Dam"), null, null)));
    }

    [Fact] // UT-28
    public async Task PlanTrip_PickupEqualsDestination_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var city = _database.City("Amsterdam");
        var dam = _database.PlaceId("De Dam");

        await Assert.ThrowsAsync<TripValidationException>(() => trips.PlanTripAsync(city.Id,
            new LocationInput(dam, null, null), new LocationInput(dam, null, null)));
    }

    [Fact] // UT-29
    public async Task PlanTrip_NoLocation_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var city = _database.City("Amsterdam");

        await Assert.ThrowsAsync<TripValidationException>(() => trips.PlanTripAsync(city.Id,
            new LocationInput(null, null, null), new LocationInput(_database.PlaceId("De Dam"), null, null)));
    }

    [Fact] // UT-30
    public async Task StartTrip_MakesTaxiUnavailableAndStartsMeter()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);

        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(trip.Rates.BaseFare, trip.CurrentPrice);
        Assert.False(db.Taxis.Single(t => t.Id == trip.TaxiId).IsAvailable);
    }

    [Fact] // UT-31
    public async Task StartTrip_Twice_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripStateException>(() => trips.StartTripAsync(trip.Id, trip.TaxiId!.Value));
    }

    [Fact] // UT-32
    public async Task StartTrip_TaxiAlreadyBusy_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var first = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        var second = await TripScenario.PlanAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripValidationException>(() => trips.StartTripAsync(second.Id, first.TaxiId!.Value));
    }

    // ---------------- Epic 2: tijdens de reis ----------------

    [Fact] // UT-33
    public async Task RecordPositions_AlongRoute_UpdatesMeterWithoutAlerts()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        var accepted = await trips.RecordPositionsAsync(trip.Id, TripScenario.DriveAlongPlannedRoute(trip, 0.5));
        var status = await trips.GetStatusAsync(trip.Id);

        Assert.True(accepted > 0);
        Assert.InRange(status.DrivenDistanceMeters, trip.PlannedDistanceMeters * 0.5 - 2, trip.PlannedDistanceMeters * 0.5 + 2);
        Assert.InRange(status.ProgressPercent, 49, 51);
        Assert.True(status.CurrentPrice > trip.Rates.BaseFare);
        Assert.Equal(trip.EstimatedPrice, status.EstimatedFinalPrice, precision: 2);
        Assert.False(status.IsOffRoute);
        Assert.Empty(status.Alerts);
    }

    [Fact] // UT-34
    public async Task RecordPositions_Detour_RaisesDeviationAndPriceAlerts()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        // De taxi rijdt 1,5 km de verkeerde kant op (ca. 4 minuten).
        var wrongWay = GeoMath.Offset(trip.Pickup, 1500, 0);
        await trips.RecordPositionsAsync(trip.Id, [new PositionReport(wrongWay, trip.StartedAt!.Value.AddMinutes(4))]);
        var status = await trips.GetStatusAsync(trip.Id);

        Assert.True(status.IsOffRoute);
        Assert.True(status.IsPriceAboveEstimate);
        Assert.Contains(status.Alerts, a => a.Type == nameof(TripAlertType.RouteDeviation));
        Assert.Contains(status.Alerts, a => a.Type == nameof(TripAlertType.PriceAboveEstimate));
    }

    [Fact] // UT-35
    public async Task RecordPositions_ImplausibleJump_IsIgnored()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        // 10 km in 10 seconden = 3600 km/u: een GPS-fout.
        var jump = GeoMath.Offset(trip.Pickup, 10_000, 0);
        var accepted = await trips.RecordPositionsAsync(trip.Id, [new PositionReport(jump, trip.StartedAt!.Value.AddSeconds(10))]);

        Assert.Equal(0, accepted);
        Assert.Equal(0, (await trips.GetStatusAsync(trip.Id)).DrivenDistanceMeters);
    }

    [Fact] // UT-36
    public async Task RecordPositions_OlderThanLastPosition_IsIgnored()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        var accepted = await trips.RecordPositionsAsync(trip.Id,
            [new PositionReport(trip.Pickup, trip.StartedAt!.Value.AddMinutes(-5))]);

        Assert.Equal(0, accepted);
    }

    [Fact] // UT-37
    public async Task RecordPositions_ForPlannedTrip_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.PlanAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripStateException>(() =>
            trips.RecordPositionsAsync(trip.Id, [new PositionReport(trip.Pickup, DateTime.UtcNow)]));
    }

    [Theory] // UT-38
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendMessage_Empty_IsRejected(string? message)
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripValidationException>(() => trips.SendPassengerMessageAsync(trip.Id, message));
    }

    [Fact] // UT-39
    public async Task SendMessage_TooLong_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripValidationException>(() =>
            trips.SendPassengerMessageAsync(trip.Id, new string('a', TripService.MaxMessageLength + 1)));
    }

    [Fact] // UT-40
    public async Task SendMessage_DuringTrip_IsVisibleAsPassengerAlert()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        await trips.SendPassengerMessageAsync(trip.Id, "  Volg de route a.u.b.  ");
        var alert = Assert.Single((await trips.GetStatusAsync(trip.Id)).Alerts);

        Assert.True(alert.FromPassenger);
        Assert.Equal("Volg de route a.u.b.", alert.Message);
    }

    // ---------------- Epic 3: na de reis ----------------

    [Fact] // UT-41
    public async Task EndTripEarly_FinalPriceIsCurrentMeterAndTaxiIsFreed()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await trips.RecordPositionsAsync(trip.Id, TripScenario.DriveAlongPlannedRoute(trip, 0.3));
        var meterBeforeEnd = (await trips.GetStatusAsync(trip.Id)).CurrentPrice;

        var ended = await trips.EndTripEarlyAsync(trip.Id);

        Assert.Equal(TripStatus.EndedEarly, ended.Status);
        Assert.Equal(meterBeforeEnd, ended.FinalPrice);
        Assert.True(ended.FinalPrice < trip.EstimatedPrice);
        Assert.True(db.Taxis.Single(t => t.Id == trip.TaxiId).IsAvailable);
        Assert.Contains(db.TripAlerts.Where(a => a.TripId == trip.Id), a => a.Type == TripAlertType.TripEndedEarly);
    }

    [Fact] // UT-42
    public async Task EndTrip_Twice_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await trips.EndTripEarlyAsync(trip.Id);

        await Assert.ThrowsAsync<TripStateException>(() => trips.EndTripEarlyAsync(trip.Id));
    }

    [Fact] // UT-43
    public async Task SubmitFeedback_AfterTrip_IsStoredOnce()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await trips.CompleteTripAsync(trip.Id);

        await trips.SubmitFeedbackAsync(trip.Id, 4, "Prima rit", reportOvercharge: false);

        var feedback = await db.TripFeedback.SingleAsync(f => f.TripId == trip.Id);
        Assert.Equal(4, feedback.Stars);
        await Assert.ThrowsAsync<TripStateException>(() => trips.SubmitFeedbackAsync(trip.Id, 5, null, false));
    }

    [Fact] // UT-44
    public async Task SubmitFeedback_DuringTrip_IsRejected()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);

        await Assert.ThrowsAsync<TripStateException>(() => trips.SubmitFeedbackAsync(trip.Id, 5, null, false));
    }

    [Theory] // UT-45
    [InlineData(0)]
    [InlineData(6)]
    public async Task SubmitFeedback_InvalidStars_IsRejected(int stars)
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await trips.CompleteTripAsync(trip.Id);

        await Assert.ThrowsAsync<TripValidationException>(() => trips.SubmitFeedbackAsync(trip.Id, stars, null, false));
    }

    [Fact] // UT-46
    public async Task UnknownTrip_ThrowsNotFound()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);

        await Assert.ThrowsAsync<TripNotFoundException>(() => trips.GetStatusAsync(Guid.NewGuid()));
    }
}

using HitchTracker.Tests.TestSupport;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Simulation;
using HitchTracker.Web.Services.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HitchTracker.Tests.Unit;

public sealed class SimulationAndRetentionTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Trip> DriveToEndAsync(bool withDetour)
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var simulator = _database.CreateSimulator(db, trips);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await simulator.CreateAsync(trip, withDetour);

        for (var tick = 0; tick < 1_000 && await simulator.StepAllAsync(TimeSpan.FromSeconds(1)) > 0; tick++)
        {
        }

        return await trips.GetTripDetailsAsync(trip.Id);
    }

    [Fact] // UT-47
    public async Task Simulation_WithoutDetour_FinalPriceEqualsEstimate()
    {
        var trip = await DriveToEndAsync(withDetour: false);

        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.FinalPrice);
        Assert.InRange(trip.FinalPrice!.Value, trip.EstimatedPrice - 0.02m, trip.EstimatedPrice + 0.02m);
        Assert.DoesNotContain(trip.Alerts, a => a.Type == TripAlertType.RouteDeviation);
        Assert.True(trip.Taxi!.IsAvailable);
    }

    [Fact] // UT-48
    public async Task Simulation_WithDetour_FinalPriceAboveEstimateAndAlertsRaised()
    {
        var trip = await DriveToEndAsync(withDetour: true);

        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.True(trip.FinalPrice > trip.EstimatedPriceHigh,
            $"final {trip.FinalPrice} high {trip.EstimatedPriceHigh} driven {trip.DrivenDistanceMeters:0} planned {trip.PlannedDistanceMeters:0} elapsed {trip.ElapsedSeconds:0} planneddur {trip.PlannedDurationSeconds:0}");
        Assert.True(trip.DrivenDistanceMeters > trip.PlannedDistanceMeters);
        Assert.Contains(trip.Alerts, a => a.Type == TripAlertType.RouteDeviation);
        Assert.Contains(trip.Alerts, a => a.Type == TripAlertType.PriceAboveEstimate);
    }

    [Fact] // UT-51 (regressietest: gevonden tijdens de acceptatietest)
    public async Task Simulation_RouteSnappedToRoad_FinalPriceStillEqualsEstimate()
    {
        // Een echte routeplanner begint de route op de dichtstbijzijnde weg, niet exact op de ophaallocatie.
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db, new SnappingRouting());
        var simulator = _database.CreateSimulator(db, trips);
        var trip = await TripScenario.StartAmsterdamTripAsync(_database, trips);
        await simulator.CreateAsync(trip, withDetour: false);

        while (await simulator.StepAllAsync(TimeSpan.FromSeconds(1)) > 0)
        {
        }

        var finished = await trips.GetTripDetailsAsync(trip.Id);
        Assert.Equal(TripStatus.Completed, finished.Status);
        Assert.InRange(finished.FinalPrice!.Value, trip.EstimatedPrice - 0.02m, trip.EstimatedPrice + 0.02m);
    }

    /// <summary>Routeplanner die begin- en eindpunt 40 m naar de "weg" verschuift, zoals OSRM doet.</summary>
    private sealed class SnappingRouting : HitchTracker.Web.Services.Routing.IRoutingService
    {
        public async Task<HitchTracker.Web.Services.Routing.RouteResult> GetRouteAsync(
            IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken = default)
        {
            var snapped = waypoints.Select(p => GeoMath.Offset(p, 40, 0)).ToList();
            return await TestDatabase.OfflineRouting.GetRouteAsync(snapped, cancellationToken);
        }
    }

    [Fact] // UT-49
    public async Task DetourPoint_LiesBesideTheRoute()
    {
        await using var db = _database.CreateContext();
        var trips = _database.CreateTripService(db);
        var trip = await TripScenario.PlanAmsterdamTripAsync(_database, trips);

        var detour = TripSimulator.DetourPointFor(trip);

        Assert.True(GeoMath.ProjectOnPath(detour, trip.PlannedRoute).DistanceFromPath > 500);
    }

    [Fact] // UT-50
    public async Task Retention_RemovesOldLocationDataButKeepsRecentTrips()
    {
        Guid oldTripId, recentTripId, staleEstimateId;
        await using (var db = _database.CreateContext())
        {
            var trips = _database.CreateTripService(db);

            var old = await TripScenario.StartAmsterdamTripAsync(_database, trips);
            await trips.RecordPositionsAsync(old.Id, TripScenario.DriveAlongPlannedRoute(old, 0.5));
            await trips.CompleteTripAsync(old.Id);
            oldTripId = old.Id;

            staleEstimateId = (await TripScenario.PlanAmsterdamTripAsync(_database, trips)).Id;

            _database.Clock.Advance(TimeSpan.FromDays(31));
            var recent = await TripScenario.StartAmsterdamTripAsync(_database, trips);
            await trips.CompleteTripAsync(recent.Id);
            recentTripId = recent.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var retention = new DataRetentionService(db, _database.Clock, Options.Create(new DataRetentionOptions()));
            var result = await retention.CleanUpAsync();

            Assert.True(result.PositionsDeleted > 0);
            Assert.Equal(1, result.PlannedTripsDeleted);
            Assert.False(await db.TripPositions.AnyAsync(p => p.TripId == oldTripId));
            Assert.True(await db.Trips.AnyAsync(t => t.Id == oldTripId)); // rit zelf blijft 90 dagen bewaard
            Assert.True(await db.TripPositions.AnyAsync(p => p.TripId == recentTripId));
            Assert.False(await db.Trips.AnyAsync(t => t.Id == staleEstimateId));
        }

        _database.Clock.Advance(TimeSpan.FromDays(90));
        await using (var db = _database.CreateContext())
        {
            var retention = new DataRetentionService(db, _database.Clock, Options.Create(new DataRetentionOptions()));
            await retention.CleanUpAsync();

            Assert.False(await db.Trips.AnyAsync(t => t.Id == oldTripId));
            Assert.False(await db.TripAlerts.AnyAsync(a => a.TripId == oldTripId));
        }
    }
}

using HitchTracker.Web.Data;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Routing;
using HitchTracker.Web.Services.Simulation;
using HitchTracker.Web.Services.Trips;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HitchTracker.Tests.TestSupport;

/// <summary>
/// Een echte SQLite-database in het geheugen met de dummydata, per test vers aangemaakt.
/// Zo testen we tegen dezelfde database-engine als de applicatie (geen EF InMemory-provider).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(DateTime? utcNow = null)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Clock = new FixedTimeProvider(utcNow ?? new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc));

        using var db = CreateContext();
        db.Database.Migrate();
        DbSeeder.SeedAsync(db).GetAwaiter().GetResult();
    }

    public FixedTimeProvider Clock { get; }

    public TripMonitoringOptions MonitoringOptions { get; } = new();

    public SimulationOptions SimulationOptions { get; } = new() { SpeedFactor = 10 };

    public HitchTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HitchTrackerDbContext>().UseSqlite(_connection).Options);

    public static IRoutingService OfflineRouting { get; } = new OfflineRoutingService(Options.Create(new RoutingOptions()));

    public TripService CreateTripService(HitchTrackerDbContext db, IRoutingService? routing = null) =>
        new(db, routing ?? OfflineRouting, new TripMonitor(Options.Create(MonitoringOptions)), Clock,
            Options.Create(MonitoringOptions), NullLogger<TripService>.Instance);

    public TripSimulator CreateSimulator(HitchTrackerDbContext db, TripService trips) =>
        new(db, OfflineRouting, trips, Options.Create(SimulationOptions), NullLogger<TripSimulator>.Instance);

    /// <summary>Plaats-id van een seed-plaats op naam.</summary>
    public int PlaceId(string name)
    {
        using var db = CreateContext();
        return db.Places.Single(p => p.Name == name).Id;
    }

    public City City(string name)
    {
        using var db = CreateContext();
        return db.Cities.Include(c => c.Tariffs).Single(c => c.Name == name);
    }

    public int AvailableTaxiId(int cityId)
    {
        using var db = CreateContext();
        return db.Taxis.First(t => t.CityId == cityId && t.IsAvailable).Id;
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>Een klok die alleen verspringt als de test dat zegt.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _now = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
}

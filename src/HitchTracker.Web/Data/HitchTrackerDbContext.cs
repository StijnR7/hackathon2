using System.Text.Json;
using HitchTracker.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HitchTracker.Web.Data;

public class HitchTrackerDbContext(DbContextOptions<HitchTrackerDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Taxi> Taxis => Set<Taxi>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripPosition> TripPositions => Set<TripPosition>();
    public DbSet<TripAlert> TripAlerts => Set<TripAlert>();
    public DbSet<TripFeedback> TripFeedback => Set<TripFeedback>();
    public DbSet<TripSimulation> TripSimulations => Set<TripSimulation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Routes (polylines) worden als JSON-tekst opgeslagen; ze worden altijd als geheel gelezen en geschreven.
        var routeConverter = new ValueConverter<List<GeoPoint>, string>(
            route => RouteJson.Serialize(route),
            json => RouteJson.Deserialize(json));
        var routeComparer = new ValueComparer<List<GeoPoint>>(
            (a, b) => a!.SequenceEqual(b!),
            route => route.Aggregate(0, (hash, point) => HashCode.Combine(hash, point.GetHashCode())),
            route => route.ToList());

        modelBuilder.Entity<City>(city =>
        {
            city.HasMany(c => c.Tariffs).WithOne(t => t.City).HasForeignKey(t => t.CityId);
            city.HasMany(c => c.Places).WithOne(p => p.City).HasForeignKey(p => p.CityId);
            city.HasMany(c => c.Taxis).WithOne(t => t.City).HasForeignKey(t => t.CityId);
        });

        modelBuilder.Entity<Taxi>(taxi =>
        {
            taxi.HasIndex(t => t.LicensePlate).IsUnique();
            taxi.HasOne(t => t.Driver).WithMany().HasForeignKey(t => t.DriverId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Trip>(trip =>
        {
            trip.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            trip.Property(t => t.PlannedRoute).HasConversion(routeConverter, routeComparer);
            trip.Property(t => t.RemainingRoute).HasConversion(routeConverter, routeComparer);
            trip.Property(t => t.Version).IsConcurrencyToken();
            trip.HasIndex(t => t.Status);

            trip.HasOne(t => t.City).WithMany().HasForeignKey(t => t.CityId).OnDelete(DeleteBehavior.Restrict);
            trip.HasOne(t => t.Taxi).WithMany().HasForeignKey(t => t.TaxiId).OnDelete(DeleteBehavior.Restrict);
            trip.HasMany(t => t.Positions).WithOne().HasForeignKey(p => p.TripId).OnDelete(DeleteBehavior.Cascade);
            trip.HasMany(t => t.Alerts).WithOne().HasForeignKey(a => a.TripId).OnDelete(DeleteBehavior.Cascade);
            trip.HasOne(t => t.Feedback).WithOne().HasForeignKey<TripFeedback>(f => f.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TripPosition>().HasIndex(p => new { p.TripId, p.RecordedAt });

        modelBuilder.Entity<TripAlert>().Property(a => a.Type).HasConversion<string>().HasMaxLength(30);

        modelBuilder.Entity<TripSimulation>(simulation =>
        {
            simulation.HasKey(s => s.TripId);
            simulation.Property(s => s.Path).HasConversion(routeConverter, routeComparer);
            simulation.HasOne<Trip>().WithOne().HasForeignKey<TripSimulation>(s => s.TripId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static class RouteJson
    {
        public static string Serialize(List<GeoPoint> route) =>
            JsonSerializer.Serialize(route.Select(p => p.ToArray()));

        public static List<GeoPoint> Deserialize(string json) =>
            (JsonSerializer.Deserialize<double[][]>(json) ?? [])
            .Where(pair => pair.Length == 2)
            .Select(pair => new GeoPoint(pair[0], pair[1]))
            .ToList();
    }
}

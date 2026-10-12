using System.ComponentModel.DataAnnotations;
using HitchTracker.Web.Data;
using HitchTracker.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services;

public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public bool Enabled { get; set; } = true;

    /// <summary>GPS-posities (de exacte gereden route) zijn locatiegegevens: na deze termijn worden ze verwijderd.</summary>
    [Range(1, 365)]
    public int PositionRetentionDays { get; set; } = 30;

    /// <summary>Afgeronde ritten (inclusief prijs en beoordeling) worden na deze termijn volledig verwijderd.</summary>
    [Range(1, 3650)]
    public int TripRetentionDays { get; set; } = 90;

    /// <summary>Schattingen waarmee nooit een rit is gestart, worden na deze termijn verwijderd.</summary>
    [Range(1, 720)]
    public int PlannedTripRetentionHours { get; set; } = 24;

    [Range(1, 168)]
    public int IntervalHours { get; set; } = 6;
}

public sealed record RetentionResult(int PositionsDeleted, int TripsDeleted, int PlannedTripsDeleted);

/// <summary>
/// AVG: dataminimalisatie en opslagbeperking. Locatiegegevens worden niet langer bewaard dan nodig
/// (bijvoorbeeld om een klacht over de ritprijs af te handelen).
/// </summary>
public class DataRetentionService(HitchTrackerDbContext db, TimeProvider clock, IOptions<DataRetentionOptions> options)
{
    public async Task<RetentionResult> CleanUpAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var positionCutoff = now.AddDays(-settings.PositionRetentionDays);
        var tripCutoff = now.AddDays(-settings.TripRetentionDays);
        var plannedCutoff = now.AddHours(-settings.PlannedTripRetentionHours);

        var positions = await db.TripPositions
            .Where(p => db.Trips.Any(t => t.Id == p.TripId && t.EndedAt != null && t.EndedAt < positionCutoff))
            .ExecuteDeleteAsync(cancellationToken);

        // Posities, meldingen, beoordeling en simulatie worden via ON DELETE CASCADE meeverwijderd.
        var trips = await db.Trips
            .Where(t => t.EndedAt != null && t.EndedAt < tripCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var planned = await db.Trips
            .Where(t => t.Status == TripStatus.Planned && t.CreatedAt < plannedCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return new RetentionResult(positions, trips, planned);
    }
}

public class DataRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DataRetentionOptions> options,
    ILogger<DataRetentionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.IntervalHours));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<DataRetentionService>().CleanUpAsync(stoppingToken);
                logger.LogInformation(
                    "Bewaartermijnen toegepast: {Positions} posities, {Trips} ritten en {Planned} schattingen verwijderd.",
                    result.PositionsDeleted, result.TripsDeleted, result.PlannedTripsDeleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Opschonen van oude ritgegevens mislukt.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

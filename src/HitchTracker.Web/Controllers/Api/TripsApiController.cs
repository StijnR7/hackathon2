using System.ComponentModel.DataAnnotations;
using HitchTracker.Web.Data;
using HitchTracker.Web.Infrastructure;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Trips;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Controllers.Api;

/// <summary>Live status van een rit, opgehaald door het scherm van de reiziger en van de chauffeur.</summary>
[ApiController]
[Route("api/trips")]
public class TripsApiController(TripService trips) : ControllerBase
{
    [HttpGet("{id:guid}/status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<TripStatusDto>> GetStatus(Guid id, CancellationToken cancellationToken) =>
        await trips.GetStatusAsync(id, cancellationToken);
}

/// <summary>
/// Koppeling voor het taxikastje of de chauffeursapp: levert GPS-posities aan en meldt aankomst.
/// Beveiligd met een API-sleutel per taxi (header <c>X-Taxi-Key</c>); een taxi kan alleen zijn eigen rit bijwerken.
/// </summary>
[ApiController]
[Route("api/taxi/trips/{id:guid}")]
public class TaxiApiController(TripService trips, HitchTrackerDbContext db) : ControllerBase
{
    public const string ApiKeyHeader = "X-Taxi-Key";

    [HttpPost("positions")]
    public async Task<IActionResult> PostPositions(Guid id, [FromHeader(Name = ApiKeyHeader)] string? apiKey,
        [FromBody] PositionBatchRequest request, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(id, apiKey, cancellationToken))
        {
            return Unauthorized();
        }

        var reports = request.Positions
            .Select(p => new PositionReport(new GeoPoint(p.Lat, p.Lng), p.RecordedAt.UtcDateTime))
            .ToList();
        var accepted = await trips.RecordPositionsAsync(id, reports, cancellationToken);
        return Ok(new { accepted, rejected = reports.Count - accepted });
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(Guid id, [FromHeader(Name = ApiKeyHeader)] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(id, apiKey, cancellationToken))
        {
            return Unauthorized();
        }

        var trip = await trips.CompleteTripAsync(id, cancellationToken);
        return Ok(new { finalPrice = trip.FinalPrice, currency = trip.CurrencyCode });
    }

    private async Task<bool> IsAuthorizedAsync(Guid tripId, string? apiKey, CancellationToken cancellationToken)
    {
        var keyHash = await db.Trips.AsNoTracking()
            .Where(t => t.Id == tripId && t.Taxi != null)
            .Select(t => t.Taxi!.ApiKeyHash)
            .SingleOrDefaultAsync(cancellationToken);
        return keyHash is not null && ApiKeyHasher.Matches(apiKey, keyHash);
    }
}

public sealed class PositionBatchRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public List<PositionRequest> Positions { get; init; } = [];
}

public sealed class PositionRequest
{
    [Range(-90, 90)]
    public double Lat { get; init; }

    [Range(-180, 180)]
    public double Lng { get; init; }

    public DateTimeOffset RecordedAt { get; init; }
}

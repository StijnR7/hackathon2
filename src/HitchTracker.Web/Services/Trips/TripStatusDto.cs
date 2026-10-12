using HitchTracker.Web.Models;

namespace HitchTracker.Web.Services.Trips;

/// <summary>
/// De live stand van een rit zoals het scherm van de reiziger (of in de taxi) die elke paar seconden ophaalt.
/// Coördinaten zijn [lat, lng]-paren, zodat Leaflet ze direct kan tekenen.
/// </summary>
public sealed record TripStatusDto(
    string Status,
    string StatusText,
    bool IsFinished,
    string CurrencyCode,
    decimal CurrentPrice,
    decimal EstimatedFinalPrice,
    decimal EstimatedPrice,
    decimal EstimatedPriceHigh,
    decimal? FinalPrice,
    double DrivenDistanceMeters,
    double RemainingDistanceMeters,
    double ElapsedSeconds,
    double RemainingDurationSeconds,
    double ProgressPercent,
    bool IsOffRoute,
    bool IsPriceAboveEstimate,
    double[]? TaxiPosition,
    IReadOnlyList<double[]> Driven,
    IReadOnlyList<double[]> Remaining,
    IReadOnlyList<TripAlertDto> Alerts)
{
    public static TripStatusDto From(Trip trip, IReadOnlyList<GeoPoint> drivenRoute)
    {
        var total = trip.DrivenDistanceMeters + trip.RemainingDistanceMeters;
        var progress = trip.Status == TripStatus.Completed ? 100
            : total > 0 ? Math.Round(trip.DrivenDistanceMeters / total * 100, 1)
            : 0;

        return new TripStatusDto(
            Status: trip.Status.ToString(),
            StatusText: trip.Status.DisplayName(),
            IsFinished: trip.IsFinished,
            CurrencyCode: trip.CurrencyCode,
            CurrentPrice: trip.FinalPrice ?? trip.CurrentPrice,
            EstimatedFinalPrice: trip.FinalPrice ?? trip.EstimatedFinalPrice,
            EstimatedPrice: trip.EstimatedPrice,
            EstimatedPriceHigh: trip.EstimatedPriceHigh,
            FinalPrice: trip.FinalPrice,
            DrivenDistanceMeters: Math.Round(trip.DrivenDistanceMeters),
            RemainingDistanceMeters: Math.Round(trip.RemainingDistanceMeters),
            ElapsedSeconds: Math.Round(trip.ElapsedSeconds),
            RemainingDurationSeconds: Math.Round(trip.RemainingDurationSeconds),
            ProgressPercent: progress,
            IsOffRoute: trip.IsOffRoute,
            IsPriceAboveEstimate: trip.IsPriceAboveEstimate,
            TaxiPosition: trip.CurrentPosition?.ToArray(),
            Driven: drivenRoute.Select(p => p.ToArray()).ToList(),
            Remaining: trip.RemainingRoute.Select(p => p.ToArray()).ToList(),
            Alerts: trip.Alerts
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                .Select(a => new TripAlertDto(a.Id, a.Type.ToString(), a.Severity, a.Message,
                    DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc), a.FromPassenger))
                .ToList());
    }
}

public sealed record TripAlertDto(int Id, string Type, string Severity, string Message, DateTime CreatedAt, bool FromPassenger);

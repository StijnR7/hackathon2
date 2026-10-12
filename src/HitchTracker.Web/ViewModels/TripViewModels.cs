using System.ComponentModel.DataAnnotations;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Trips;

namespace HitchTracker.Web.ViewModels;

public class StartTripForm
{
    [Required(ErrorMessage = "Kies de taxi waarin je stapt.")]
    public int? TaxiId { get; set; }

    /// <summary>Demo: laat de gesimuleerde chauffeur omrijden.</summary>
    public bool SimulateDetour { get; set; }
}

public class MessageForm
{
    [StringLength(TripService.MaxMessageLength, ErrorMessage = "Een bericht mag maximaal {1} tekens lang zijn.")]
    public string? Message { get; set; }
}

public class FeedbackForm
{
    [Range(1, 5, ErrorMessage = "Geef een beoordeling van 1 tot en met 5 sterren.")]
    public int Stars { get; set; }

    [StringLength(TripService.MaxCommentLength, ErrorMessage = "Een toelichting mag maximaal {1} tekens lang zijn.")]
    public string? Comment { get; set; }

    public bool ReportOvercharge { get; set; }
}

public class TripEstimateViewModel
{
    public required Trip Trip { get; init; }
    public required IReadOnlyList<Taxi> AvailableTaxis { get; init; }
    public StartTripForm Form { get; init; } = new();

    public FareBreakdown Breakdown => Trip.Rates.Calculate(Trip.PlannedDistanceMeters, Trip.PlannedDurationSeconds);

    public object MapData => TripMapData.For(Trip, drivenRoute: []);
}

public class LiveTripViewModel
{
    public required Trip Trip { get; init; }
    public required IReadOnlyList<GeoPoint> DrivenRoute { get; init; }
    public MessageForm Form { get; init; } = new();
    public IReadOnlyList<string> QuickMessages => TripService.QuickMessages;

    public object MapData => TripMapData.For(Trip, DrivenRoute);
}

public class TripSummaryViewModel
{
    public required Trip Trip { get; init; }
    public required IReadOnlyList<GeoPoint> DrivenRoute { get; init; }
    public FeedbackForm Form { get; init; } = new();

    public decimal FinalPrice => Trip.FinalPrice ?? Trip.CurrentPrice;

    public FareBreakdown Breakdown => Trip.Rates.Calculate(Trip.DrivenDistanceMeters, Trip.ElapsedSeconds);

    public bool IsFair => FinalPrice <= Trip.EstimatedPriceHigh;

    public decimal DifferenceFromEstimate => FinalPrice - Trip.EstimatedPrice;

    public object MapData => TripMapData.For(Trip, DrivenRoute);
}

/// <summary>Gegevens voor de kaart op de ritpagina's (trip.js).</summary>
public static class TripMapData
{
    public static object For(Trip trip, IReadOnlyList<GeoPoint> drivenRoute) => new
    {
        pickup = trip.Pickup.ToArray(),
        destination = trip.Destination.ToArray(),
        planned = trip.PlannedRoute.Select(p => p.ToArray()),
        driven = drivenRoute.Select(p => p.ToArray()),
        remaining = trip.RemainingRoute.Select(p => p.ToArray()),
        taxi = trip.CurrentPosition?.ToArray(),
        currency = trip.CurrencyCode,
        status = trip.Status.ToString()
    };
}

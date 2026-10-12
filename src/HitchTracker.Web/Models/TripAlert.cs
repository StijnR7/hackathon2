using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

public enum TripAlertType
{
    RouteDeviation,
    BackOnRoute,
    PriceAboveEstimate,
    PassengerMessage,
    TripEndedEarly
}

/// <summary>Een melding tijdens de rit: een waarschuwing van het systeem of een bericht van de reiziger aan de chauffeur.</summary>
public class TripAlert
{
    public int Id { get; set; }
    public Guid TripId { get; set; }
    public TripAlertType Type { get; set; }

    [MaxLength(300)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool FromPassenger => Type == TripAlertType.PassengerMessage;

    /// <summary>Bootstrap-kleur voor de weergave.</summary>
    public string Severity => Type switch
    {
        TripAlertType.RouteDeviation or TripAlertType.PriceAboveEstimate => "danger",
        TripAlertType.BackOnRoute => "success",
        TripAlertType.TripEndedEarly => "warning",
        _ => "info"
    };
}

namespace HitchTracker.Web.Models;

/// <summary>Een GPS-positie van de taxi tijdens een rit. Samen vormen ze de gereden route.</summary>
public class TripPosition
{
    public long Id { get; set; }
    public Guid TripId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime RecordedAt { get; set; }

    /// <summary>Afstand tot de geplande route op het moment van meten.</summary>
    public double DistanceFromRouteMeters { get; set; }

    public GeoPoint Location => new(Lat, Lng);
}

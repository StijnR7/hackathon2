using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>
/// Een taxirit: van prijsschatting (voor de reis), via live volgen (tijdens de reis) tot eindprijs (na de reis).
/// De <see cref="Id"/> is een willekeurige GUID en fungeert als geheime sleutel: wie de link heeft, kan de rit volgen.
/// Er worden geen persoonsgegevens van de reiziger opgeslagen (geen account nodig).
/// </summary>
public class Trip
{
    public Guid Id { get; set; }

    public int CityId { get; set; }
    public City City { get; set; } = null!;

    public int? TaxiId { get; set; }
    public Taxi? Taxi { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Planned;

    // --- Voor de reis: route en schatting ---

    [MaxLength(120)]
    public string PickupName { get; set; } = string.Empty;
    public double PickupLat { get; set; }
    public double PickupLng { get; set; }

    [MaxLength(120)]
    public string DestinationName { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }

    public List<GeoPoint> PlannedRoute { get; set; } = [];
    public double PlannedDistanceMeters { get; set; }
    public double PlannedDurationSeconds { get; set; }

    /// <summary>Welke routeplanner de route heeft berekend (online OSRM of offline benadering).</summary>
    [MaxLength(40)]
    public string RouteSource { get; set; } = string.Empty;

    [MaxLength(60)]
    public string TariffName { get; set; } = string.Empty;

    /// <summary>Kopie van het tarief op het moment van plannen.</summary>
    public FareRates Rates { get; set; } = new();

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Schatting bij normaal verkeer.</summary>
    public decimal EstimatedPrice { get; set; }

    /// <summary>Bovengrens van de schatting (druk verkeer). Daarboven waarschuwt de app.</summary>
    public decimal EstimatedPriceHigh { get; set; }

    // --- Tijdens de reis: meter en voortgang ---

    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTime? LastPositionAt { get; set; }

    public double DrivenDistanceMeters { get; set; }

    /// <summary>Verstreken ritduur volgens de GPS-tijdstempels.</summary>
    public double ElapsedSeconds { get; set; }

    /// <summary>Hoe ver de taxi over de geplande route is gekomen.</summary>
    public double ProgressAlongRouteMeters { get; set; }

    public List<GeoPoint> RemainingRoute { get; set; } = [];
    public double RemainingDistanceMeters { get; set; }
    public double RemainingDurationSeconds { get; set; }
    public DateTime? RemainingRouteCalculatedAt { get; set; }

    public decimal CurrentPrice { get; set; }
    public decimal EstimatedFinalPrice { get; set; }

    public bool IsOffRoute { get; set; }
    public bool PriceAlertSent { get; set; }

    // --- Na de reis ---

    public decimal? FinalPrice { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Optimistic concurrency: de simulator en de reiziger kunnen tegelijk een rit wijzigen.</summary>
    public int Version { get; set; }

    public ICollection<TripPosition> Positions { get; set; } = new List<TripPosition>();
    public ICollection<TripAlert> Alerts { get; set; } = new List<TripAlert>();
    public TripFeedback? Feedback { get; set; }

    public GeoPoint Pickup => new(PickupLat, PickupLng);
    public GeoPoint Destination => new(DestinationLat, DestinationLng);
    public GeoPoint? CurrentPosition => CurrentLat is { } lat && CurrentLng is { } lng ? new GeoPoint(lat, lng) : null;

    public bool IsFinished => Status is TripStatus.Completed or TripStatus.EndedEarly;

    /// <summary>Ligt de (verwachte) eindprijs boven de bovengrens van de schatting?</summary>
    public bool IsPriceAboveEstimate => (FinalPrice ?? EstimatedFinalPrice) > EstimatedPriceHigh;
}

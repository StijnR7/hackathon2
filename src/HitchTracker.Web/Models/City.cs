using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>Een stad waarin HitchTracker actief is, met eigen valuta, tijdzone en tarieven.</summary>
public class City
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Country { get; set; } = string.Empty;

    /// <summary>ISO 4217 valutacode, bijv. EUR of GBP.</summary>
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>IANA-tijdzone, bijv. Europe/London. Nodig om het juiste dag- of nachttarief te kiezen.</summary>
    [MaxLength(64)]
    public string TimeZoneId { get; set; } = "UTC";

    public double CenterLat { get; set; }
    public double CenterLng { get; set; }

    /// <summary>Straal van het werkgebied rond het centrum.</summary>
    public double ServiceRadiusKm { get; set; }

    public ICollection<Tariff> Tariffs { get; set; } = new List<Tariff>();
    public ICollection<Place> Places { get; set; } = new List<Place>();
    public ICollection<Taxi> Taxis { get; set; } = new List<Taxi>();

    public GeoPoint Center => new(CenterLat, CenterLng);

    /// <summary>Zet een UTC-tijdstip om naar de lokale tijd van de stad.</summary>
    public DateTime ToLocalTime(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(asUtc, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Onbekende tijdzone op deze server: liever UTC tonen dan crashen.
            return asUtc;
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>
/// Een officieel taxitarief van een stad dat geldt tussen <see cref="StartHour"/> en <see cref="EndHour"/> (lokale tijd).
/// </summary>
public class Tariff
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public City City { get; set; } = null!;

    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Beginuur (inclusief), 0-23.</summary>
    [Range(0, 23)]
    public int StartHour { get; set; }

    /// <summary>Einduur (exclusief), 0-23. Gelijk aan <see cref="StartHour"/> betekent: de hele dag.</summary>
    [Range(0, 23)]
    public int EndHour { get; set; }

    public FareRates Rates { get; set; } = new();

    public bool IsAllDay => StartHour == EndHour;

    /// <summary>Geldt dit tarief op het gegeven (lokale) uur? Ondersteunt periodes over middernacht, bijv. 20-6.</summary>
    public bool AppliesAt(int localHour)
    {
        if (IsAllDay)
        {
            return true;
        }

        return StartHour < EndHour
            ? localHour >= StartHour && localHour < EndHour
            : localHour >= StartHour || localHour < EndHour;
    }
}

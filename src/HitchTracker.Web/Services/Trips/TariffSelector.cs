using HitchTracker.Web.Models;

namespace HitchTracker.Web.Services.Trips;

public static class TariffSelector
{
    /// <summary>
    /// Kiest het tarief dat op dit moment (lokale tijd van de stad) geldt.
    /// Een tarief voor een specifieke periode (bijv. nacht) gaat voor op een tarief dat de hele dag geldt.
    /// </summary>
    public static Tariff? Select(City city, DateTime utcNow)
    {
        var localHour = city.ToLocalTime(utcNow).Hour;
        return city.Tariffs
            .Where(t => t.AppliesAt(localHour))
            .OrderBy(t => t.IsAllDay)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
    }
}

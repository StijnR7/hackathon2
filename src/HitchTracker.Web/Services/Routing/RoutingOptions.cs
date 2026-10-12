using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Services.Routing;

public enum RoutingProvider
{
    /// <summary>Echte wegenroute via een OSRM-server (OpenStreetMap-data).</summary>
    Osrm,

    /// <summary>Offline benadering zonder internet; gebruikt in tests en als terugvaloptie.</summary>
    Offline
}

public class RoutingOptions
{
    public const string SectionName = "Routing";

    public RoutingProvider Provider { get; set; } = RoutingProvider.Osrm;

    [Url]
    public string OsrmBaseUrl { get; set; } = "https://router.project-osrm.org/";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>Gemiddelde snelheid in de stad voor de offline benadering.</summary>
    [Range(1, 130)]
    public double OfflineAverageSpeedKmh { get; set; } = 25;
}

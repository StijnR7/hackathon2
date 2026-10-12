using HitchTracker.Web.Models;
using Microsoft.Extensions.Options;

namespace HitchTracker.Web.Services.Routing;

/// <summary>
/// Routeplanner zonder internetverbinding. Rijdt eerst noord/zuid en dan oost/west ("Manhattan-route"),
/// wat in een stad een redelijke benadering is van de afstand over de weg (ca. 1,3x hemelsbreed).
/// Wordt gebruikt in de geautomatiseerde tests en als terugvaloptie als de online routeplanner niet bereikbaar is.
/// </summary>
public class OfflineRoutingService(IOptions<RoutingOptions> options) : IRoutingService
{
    public const string SourceName = "Offline benadering";

    public Task<RouteResult> GetRouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken = default)
    {
        RouteValidation.EnsureValidWaypoints(waypoints);

        var points = new List<GeoPoint> { waypoints[0] };
        for (var i = 1; i < waypoints.Count; i++)
        {
            var from = waypoints[i - 1];
            var to = waypoints[i];
            points.Add(new GeoPoint(to.Lat, from.Lng));
            points.Add(to);
        }

        var distance = GeoMath.PathLength(points);
        var metersPerSecond = options.Value.OfflineAverageSpeedKmh / 3.6;
        return Task.FromResult(new RouteResult(points, distance, distance / metersPerSecond, SourceName));
    }
}

internal static class RouteValidation
{
    public static void EnsureValidWaypoints(IReadOnlyList<GeoPoint> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        if (waypoints.Count < 2)
        {
            throw new ArgumentException("Een route heeft minimaal een start- en eindpunt nodig.", nameof(waypoints));
        }

        if (waypoints.Any(p => !p.IsValid))
        {
            throw new ArgumentException("De route bevat een ongeldige coördinaat.", nameof(waypoints));
        }
    }
}

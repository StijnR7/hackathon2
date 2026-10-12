using System.Globalization;
using System.Text.Json.Serialization;
using HitchTracker.Web.Models;

namespace HitchTracker.Web.Services.Routing;

/// <summary>
/// Haalt een echte wegenroute op bij een OSRM-server (Open Source Routing Machine, OpenStreetMap-data).
/// Is de server niet bereikbaar, dan valt de service terug op <see cref="OfflineRoutingService"/>,
/// zodat de reiziger altijd een schatting krijgt.
/// </summary>
public class OsrmRoutingService(
    HttpClient httpClient,
    OfflineRoutingService fallback,
    ILogger<OsrmRoutingService> logger) : IRoutingService
{
    public const string SourceName = "OpenStreetMap (OSRM)";

    public async Task<RouteResult> GetRouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken = default)
    {
        RouteValidation.EnsureValidWaypoints(waypoints);

        try
        {
            return await RequestRouteAsync(waypoints, cancellationToken);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or RoutingException or System.Text.Json.JsonException)
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OSRM-route niet beschikbaar, offline benadering wordt gebruikt.");
            return await fallback.GetRouteAsync(waypoints, cancellationToken);
        }
    }

    private async Task<RouteResult> RequestRouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        // OSRM verwacht "lng,lat;lng,lat" met een punt als decimaalteken, ongeacht de cultuur van de server.
        var coordinateList = string.Join(';', waypoints.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.Lng:0.######},{p.Lat:0.######}")));
        var url = $"route/v1/driving/{coordinateList}?overview=full&geometries=geojson";

        var response = await httpClient.GetFromJsonAsync<OsrmResponse>(url, cancellationToken)
                       ?? throw new RoutingException("Leeg antwoord van de routeplanner.");

        var route = response.Code == "Ok" ? response.Routes?.FirstOrDefault() : null;
        if (route?.Geometry?.Coordinates is not { Count: >= 2 } routeCoordinates)
        {
            throw new RoutingException($"Routeplanner gaf geen route terug (code: {response.Code}).");
        }

        var points = routeCoordinates
            .Where(c => c.Length >= 2)
            .Select(c => new GeoPoint(c[1], c[0]))
            .ToList();

        return new RouteResult(points, route.Distance, route.Duration, SourceName);
    }

    private sealed record OsrmResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("routes")] List<OsrmRoute>? Routes);

    private sealed record OsrmRoute(
        [property: JsonPropertyName("distance")] double Distance,
        [property: JsonPropertyName("duration")] double Duration,
        [property: JsonPropertyName("geometry")] OsrmGeometry? Geometry);

    private sealed record OsrmGeometry(
        [property: JsonPropertyName("coordinates")] List<double[]>? Coordinates);
}

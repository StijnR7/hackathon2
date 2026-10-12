using HitchTracker.Web.Models;

namespace HitchTracker.Web.Services.Routing;

/// <summary>Berekent een route over de weg tussen twee of meer punten.</summary>
public interface IRoutingService
{
    /// <param name="waypoints">Start, eventuele tussenpunten en eindpunt (minimaal twee).</param>
    Task<RouteResult> GetRouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken = default);
}

/// <param name="Points">De route als polyline.</param>
/// <param name="DistanceMeters">Afstand over de weg.</param>
/// <param name="DurationSeconds">Verwachte rijtijd bij normaal verkeer.</param>
/// <param name="Source">Welke routeplanner de route heeft berekend (voor uitleg aan de gebruiker).</param>
public sealed record RouteResult(List<GeoPoint> Points, double DistanceMeters, double DurationSeconds, string Source);

/// <summary>Er kon geen route worden berekend.</summary>
public class RoutingException(string message, Exception? innerException = null) : Exception(message, innerException);

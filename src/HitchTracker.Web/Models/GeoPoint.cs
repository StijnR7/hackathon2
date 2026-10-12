namespace HitchTracker.Web.Models;

/// <summary>
/// Een coördinaat (WGS84) in decimale graden.
/// </summary>
public readonly record struct GeoPoint(double Lat, double Lng)
{
    public bool IsValid =>
        !double.IsNaN(Lat) && !double.IsNaN(Lng) &&
        Lat is >= -90 and <= 90 &&
        Lng is >= -180 and <= 180;

    /// <summary>Formaat dat Leaflet direct begrijpt: [lat, lng].</summary>
    public double[] ToArray() => [Lat, Lng];
}

using HitchTracker.Web.Models;

namespace HitchTracker.Web.Services;

/// <summary>
/// Rekenwerk met coördinaten en routes (polylines).
/// Afstanden zijn in meters. Voor korte stukken (een stad) is een lokale vlakke projectie nauwkeurig genoeg.
/// </summary>
public static class GeoMath
{
    public const double EarthRadiusMeters = 6_371_000;

    /// <summary>Afstand over het aardoppervlak (haversine-formule).</summary>
    public static double Distance(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRadians(b.Lat - a.Lat);
        var dLng = ToRadians(b.Lng - a.Lng);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(a.Lat)) * Math.Cos(ToRadians(b.Lat)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static double PathLength(IReadOnlyList<GeoPoint> path)
    {
        var length = 0d;
        for (var i = 1; i < path.Count; i++)
        {
            length += Distance(path[i - 1], path[i]);
        }

        return length;
    }

    public static GeoPoint Interpolate(GeoPoint a, GeoPoint b, double fraction) =>
        new(a.Lat + (b.Lat - a.Lat) * fraction, a.Lng + (b.Lng - a.Lng) * fraction);

    /// <summary>
    /// Verschuift een punt een aantal meters naar het noorden en oosten (negatief = zuid/west).
    /// </summary>
    public static GeoPoint Offset(GeoPoint origin, double northMeters, double eastMeters)
    {
        var dLat = northMeters / EarthRadiusMeters;
        var dLng = eastMeters / (EarthRadiusMeters * Math.Cos(ToRadians(origin.Lat)));
        return new GeoPoint(origin.Lat + ToDegrees(dLat), origin.Lng + ToDegrees(dLng));
    }

    /// <summary>Het punt dat <paramref name="distanceAlong"/> meter vanaf het begin over het pad ligt.</summary>
    public static GeoPoint PointAlongPath(IReadOnlyList<GeoPoint> path, double distanceAlong)
    {
        EnsureNotEmpty(path);
        if (distanceAlong <= 0 || path.Count == 1)
        {
            return path[0];
        }

        var travelled = 0d;
        for (var i = 1; i < path.Count; i++)
        {
            var segment = Distance(path[i - 1], path[i]);
            if (travelled + segment >= distanceAlong)
            {
                var fraction = segment == 0 ? 0 : (distanceAlong - travelled) / segment;
                return Interpolate(path[i - 1], path[i], fraction);
            }

            travelled += segment;
        }

        return path[^1];
    }

    /// <summary>
    /// Het deel van het pad tussen twee afstanden, inclusief de tussenliggende hoekpunten.
    /// Zo volgt een gereden stuk precies de bochten van de weg.
    /// </summary>
    public static List<GeoPoint> SubPath(IReadOnlyList<GeoPoint> path, double fromDistance, double toDistance)
    {
        EnsureNotEmpty(path);
        if (toDistance < fromDistance)
        {
            throw new ArgumentException("Het einde van het deelpad ligt voor het begin.", nameof(toDistance));
        }

        var result = new List<GeoPoint> { PointAlongPath(path, fromDistance) };
        var travelled = 0d;
        for (var i = 1; i < path.Count; i++)
        {
            travelled += Distance(path[i - 1], path[i]);
            if (travelled > fromDistance && travelled < toDistance)
            {
                result.Add(path[i]);
            }
        }

        result.Add(PointAlongPath(path, toDistance));
        return result;
    }

    /// <summary>Het resterende deel van het pad vanaf een bepaalde afstand.</summary>
    public static List<GeoPoint> RemainingPath(IReadOnlyList<GeoPoint> path, double fromDistance) =>
        SubPath(path, fromDistance, Math.Max(fromDistance, PathLength(path)));

    /// <summary>
    /// Projecteert een punt op het dichtstbijzijnde stuk van het pad.
    /// Met <paramref name="minDistanceAlong"/> worden stukken die al verreden zijn overgeslagen,
    /// zodat een route die vlak langs zichzelf terugloopt niet tot een sprong in de voortgang leidt.
    /// </summary>
    public static PathProjection ProjectOnPath(GeoPoint point, IReadOnlyList<GeoPoint> path, double minDistanceAlong = 0)
    {
        EnsureNotEmpty(path);
        if (path.Count == 1)
        {
            return new PathProjection(0, Distance(point, path[0]));
        }

        var best = new PathProjection(0, double.MaxValue);
        var travelled = 0d;
        for (var i = 1; i < path.Count; i++)
        {
            var segmentLength = Distance(path[i - 1], path[i]);
            var segmentEnd = travelled + segmentLength;
            if (segmentEnd >= minDistanceAlong)
            {
                var (fraction, distance) = ProjectOnSegment(point, path[i - 1], path[i]);
                var along = Math.Max(minDistanceAlong, travelled + fraction * segmentLength);
                if (distance < best.DistanceFromPath)
                {
                    best = new PathProjection(along, distance);
                }
            }

            travelled = segmentEnd;
        }

        return best.DistanceFromPath == double.MaxValue
            ? new PathProjection(travelled, Distance(point, path[^1]))
            : best;
    }

    private static (double Fraction, double Distance) ProjectOnSegment(GeoPoint p, GeoPoint a, GeoPoint b)
    {
        // Lokale vlakke projectie (equirectangular) met p als oorsprong, in meters.
        var cosLat = Math.Cos(ToRadians(p.Lat));
        var ax = ToRadians(a.Lng - p.Lng) * cosLat * EarthRadiusMeters;
        var ay = ToRadians(a.Lat - p.Lat) * EarthRadiusMeters;
        var bx = ToRadians(b.Lng - p.Lng) * cosLat * EarthRadiusMeters;
        var by = ToRadians(b.Lat - p.Lat) * EarthRadiusMeters;

        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / lengthSquared, 0, 1);

        var closestX = ax + t * dx;
        var closestY = ay + t * dy;
        return (t, Math.Sqrt(closestX * closestX + closestY * closestY));
    }

    private static void EnsureNotEmpty(IReadOnlyList<GeoPoint> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Count == 0)
        {
            throw new ArgumentException("Een pad moet minstens één punt bevatten.", nameof(path));
        }
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}

/// <param name="DistanceAlongPath">Afstand vanaf het begin van het pad tot het geprojecteerde punt.</param>
/// <param name="DistanceFromPath">Kortste afstand van het punt tot het pad.</param>
public readonly record struct PathProjection(double DistanceAlongPath, double DistanceFromPath);

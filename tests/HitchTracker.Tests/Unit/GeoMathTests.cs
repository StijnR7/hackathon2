using HitchTracker.Web.Models;
using HitchTracker.Web.Services;

namespace HitchTracker.Tests.Unit;

public class GeoMathTests
{
    // Een rechte lijn van ca. 1 km naar het oosten in Amsterdam, en dan 1 km naar het noorden.
    private static readonly GeoPoint Start = new(52.3700, 4.8900);
    private static readonly GeoPoint East = GeoMath.Offset(Start, 0, 1000);
    private static readonly GeoPoint NorthEast = GeoMath.Offset(East, 1000, 0);
    private static readonly List<GeoPoint> LShapedPath = [Start, East, NorthEast];

    [Fact] // UT-01
    public void Distance_OneDegreeLongitudeAtEquator_IsAbout111Km()
    {
        var distance = GeoMath.Distance(new GeoPoint(0, 0), new GeoPoint(0, 1));

        Assert.InRange(distance, 111_150, 111_250);
    }

    [Fact] // UT-02
    public void Distance_KnownLandmarks_MatchesReference()
    {
        // Big Ben - Tower of London is hemelsbreed ca. 3,4 km.
        var distance = GeoMath.Distance(new GeoPoint(51.50073, -0.12463), new GeoPoint(51.50812, -0.07595));

        Assert.InRange(distance, 3_300, 3_500);
    }

    [Fact] // UT-03
    public void PathLength_SumsAllSegments()
    {
        Assert.InRange(GeoMath.PathLength(LShapedPath), 1_999, 2_001);
    }

    [Theory] // UT-04
    [InlineData(0, 0)]
    [InlineData(500, 500)]
    [InlineData(1500, 1500)]
    [InlineData(99_999, 2000)]
    public void PointAlongPath_ReturnsPointAtDistance(double distanceAlong, double expectedFromStart)
    {
        var point = GeoMath.PointAlongPath(LShapedPath, distanceAlong);
        var projection = GeoMath.ProjectOnPath(point, LShapedPath);

        Assert.InRange(projection.DistanceAlongPath, expectedFromStart - 1, expectedFromStart + 1);
        Assert.True(projection.DistanceFromPath < 1);
    }

    [Fact] // UT-05
    public void ProjectOnPath_PointBesidePath_ReturnsPerpendicularDistance()
    {
        var beside = GeoMath.Offset(GeoMath.PointAlongPath(LShapedPath, 400), -150, 0); // 150 m ten zuiden van de route

        var projection = GeoMath.ProjectOnPath(beside, LShapedPath);

        Assert.InRange(projection.DistanceFromPath, 149, 151);
        Assert.InRange(projection.DistanceAlongPath, 399, 401);
    }

    [Fact] // UT-06
    public void ProjectOnPath_IgnoresSegmentsBeforeMinimumProgress()
    {
        // Een punt vlak bij het begin, maar we zijn al 1500 m onderweg: de projectie mag niet terugspringen.
        var nearStart = GeoMath.Offset(Start, 10, 10);

        var projection = GeoMath.ProjectOnPath(nearStart, LShapedPath, minDistanceAlong: 1500);

        Assert.True(projection.DistanceAlongPath >= 1500);
    }

    [Fact] // UT-07
    public void SubPath_KeepsCornerPoints()
    {
        var sub = GeoMath.SubPath(LShapedPath, 500, 1500);

        Assert.Equal(3, sub.Count); // begin, hoekpunt, eind
        Assert.Equal(East, sub[1]);
        Assert.InRange(GeoMath.PathLength(sub), 999, 1001);
    }

    [Fact] // UT-08
    public void RemainingPath_FromHalfway_IsHalfTheLength()
    {
        var remaining = GeoMath.RemainingPath(LShapedPath, 1000);

        Assert.InRange(GeoMath.PathLength(remaining), 999, 1001);
        Assert.Equal(NorthEast, remaining[^1]);
    }

    [Fact] // UT-09
    public void EmptyPath_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => GeoMath.PointAlongPath([], 10));
        Assert.Throws<ArgumentException>(() => GeoMath.ProjectOnPath(Start, []));
    }

    [Theory] // UT-10
    [InlineData(52.37, 4.89, true)]
    [InlineData(91, 4.89, false)]
    [InlineData(52.37, -181, false)]
    [InlineData(double.NaN, 4.89, false)]
    public void GeoPoint_IsValid_ChecksRanges(double lat, double lng, bool expected)
    {
        Assert.Equal(expected, new GeoPoint(lat, lng).IsValid);
    }
}

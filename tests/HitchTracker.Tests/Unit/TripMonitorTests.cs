using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Trips;
using Microsoft.Extensions.Options;

namespace HitchTracker.Tests.Unit;

public class TripMonitorTests
{
    private static readonly GeoPoint Start = new(51.5000, -0.1200);
    private static readonly GeoPoint End = GeoMath.Offset(Start, 0, 2000);

    private readonly TripMonitor _monitor = new(Options.Create(new TripMonitoringOptions
    {
        OffRouteThresholdMeters = 100,
        BackOnRouteThresholdMeters = 50
    }));

    private static Trip TripOnStraightRoad() => new()
    {
        PlannedRoute = [Start, End],
        PlannedDistanceMeters = 2000
    };

    private static GeoPoint PointBesideRoute(double alongMeters, double besideMeters) =>
        GeoMath.Offset(GeoMath.Offset(Start, 0, alongMeters), besideMeters, 0);

    [Fact] // UT-19
    public void OnRoute_NoAlert()
    {
        var check = _monitor.CheckPosition(TripOnStraightRoad(), PointBesideRoute(800, 20));

        Assert.False(check.IsOffRoute);
        Assert.False(check.LeftRoute);
        Assert.InRange(check.DistanceAlongRouteMeters, 799, 801);
    }

    [Fact] // UT-20
    public void MoreThanThresholdFromRoute_LeavesRoute()
    {
        var check = _monitor.CheckPosition(TripOnStraightRoad(), PointBesideRoute(800, 150));

        Assert.True(check.IsOffRoute);
        Assert.True(check.LeftRoute);
        Assert.InRange(check.DistanceFromRouteMeters, 149, 151);
    }

    [Fact] // UT-21
    public void AlreadyOffRoute_BetweenThresholds_StaysOffRoute()
    {
        // Hysterese: 75 m is binnen de 100 m-drempel, maar nog niet terug binnen 50 m.
        var trip = TripOnStraightRoad();
        trip.IsOffRoute = true;

        var check = _monitor.CheckPosition(trip, PointBesideRoute(900, 75));

        Assert.True(check.IsOffRoute);
        Assert.False(check.LeftRoute);
        Assert.False(check.ReturnedToRoute);
    }

    [Fact] // UT-22
    public void AlreadyOffRoute_BackWithinThreshold_ReturnsToRoute()
    {
        var trip = TripOnStraightRoad();
        trip.IsOffRoute = true;

        var check = _monitor.CheckPosition(trip, PointBesideRoute(1000, 10));

        Assert.False(check.IsOffRoute);
        Assert.True(check.ReturnedToRoute);
    }

    [Fact] // UT-23
    public void TripWithoutRoute_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _monitor.CheckPosition(new Trip(), Start));
    }
}

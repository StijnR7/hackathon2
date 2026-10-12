using System.Net;
using System.Text;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HitchTracker.Tests.Unit;

public class RoutingTests
{
    private static readonly GeoPoint From = new(51.50073, -0.12463);
    private static readonly GeoPoint To = new(51.50812, -0.07595);
    private static readonly OfflineRoutingService Offline = new(Options.Create(new RoutingOptions()));

    private static OsrmRoutingService CreateOsrm(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://osrm.test/") }, Offline,
            NullLogger<OsrmRoutingService>.Instance);

    [Fact] // UT-52
    public async Task Osrm_ParsesRouteAndSendsLngLatWithDecimalPoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            { "code": "Ok", "routes": [ { "distance": 4567.8, "duration": 712.3,
              "geometry": { "coordinates": [[-0.12463, 51.50073], [-0.1, 51.505], [-0.07595, 51.50812]] } } ] }
            """);

        var route = await CreateOsrm(handler).GetRouteAsync([From, To]);

        Assert.Equal(OsrmRoutingService.SourceName, route.Source);
        Assert.Equal(3, route.Points.Count);
        Assert.Equal(new GeoPoint(51.505, -0.1), route.Points[1]); // [lng, lat] omgezet naar (lat, lng)
        Assert.Equal(712.3, route.DurationSeconds);
        Assert.Contains("route/v1/driving/-0.12463,51.50073;-0.07595,51.50812", handler.LastRequestUri);
    }

    [Theory] // UT-53
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, """{ "code": "NoRoute", "routes": [] }""")]
    [InlineData(HttpStatusCode.OK, "dit is geen json")]
    public async Task Osrm_Unavailable_FallsBackToOfflineRoute(HttpStatusCode status, string body)
    {
        var route = await CreateOsrm(new StubHandler(status, body)).GetRouteAsync([From, To]);

        Assert.Equal(OfflineRoutingService.SourceName, route.Source);
        Assert.True(route.DistanceMeters > GeoMath.Distance(From, To)); // over de weg langer dan hemelsbreed
    }

    [Fact] // UT-54
    public async Task Offline_RouteLengthMatchesPolylineAndSpeed()
    {
        var route = await Offline.GetRouteAsync([From, To]);

        Assert.Equal(GeoMath.PathLength(route.Points), route.DistanceMeters, precision: 6);
        Assert.Equal(route.DistanceMeters / (25 / 3.6), route.DurationSeconds, precision: 6);
    }

    [Fact] // UT-55
    public async Task Routing_RejectsInvalidWaypoints()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Offline.GetRouteAsync([From]));
        await Assert.ThrowsAsync<ArgumentException>(() => Offline.GetRouteAsync([From, new GeoPoint(95, 0)]));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string LastRequestUri { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

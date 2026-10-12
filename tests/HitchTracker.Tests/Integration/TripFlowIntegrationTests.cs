using System.Net;
using System.Net.Http.Json;
using HitchTracker.Web.Controllers.Api;
using HitchTracker.Web.Data;
using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Simulation;
using HitchTracker.Web.Services.Trips;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HitchTracker.Tests.Integration;

/// <summary>
/// Integratietests: de onderdelen (controllers, views, services, database, API) samen, via echte HTTP-verzoeken.
/// </summary>
public sealed class TripFlowIntegrationTests : IClassFixture<HitchTrackerWebFactory>
{
    private readonly HitchTrackerWebFactory _factory;

    public TripFlowIntegrationTests(HitchTrackerWebFactory factory) => _factory = factory;

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact] // IT-01
    public async Task HomePage_ShowsCitiesAndPlaces()
    {
        var client = CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("Waar wil je heen?", html);
        Assert.Contains("Londen (Verenigd Koninkrijk)", html);
        Assert.Contains("Big Ben", html);
    }

    [Fact] // IT-02
    public async Task CompleteTrip_FromEstimateToFinalPrice()
    {
        var client = CreateClient();
        var (cityId, pickupId, destinationId) = await PlacesAsync("Amsterdam", "Amsterdam Centraal", "Rijksmuseum");

        // Epic 1: schatting
        var estimateUrl = await PlanTripAsync(client, cityId, pickupId, destinationId);
        var estimateHtml = await client.GetStringAsync(estimateUrl);
        Assert.Contains("Verwachte prijs", estimateHtml);
        Assert.Contains("€", estimateHtml);

        // Instappen
        var tripId = TripIdFrom(estimateUrl);
        var taxiId = await AvailableTaxiAsync(cityId);
        var start = await PostFormAsync(client, estimateUrl, $"/ritten/{tripId}/start", new()
        {
            ["Form.TaxiId"] = taxiId.ToString()
        });
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.Equal($"/ritten/{tripId}", start.Headers.Location?.OriginalString);

        // Epic 2: live volgen (de simulator rijdt de taxi; de test bepaalt het tempo)
        var liveHtml = await client.GetStringAsync($"/ritten/{tripId}");
        Assert.Contains("Huidige prijs", liveHtml);
        await DriveSimulationAsync(ticks: 5);

        var status = await client.GetFromJsonAsync<TripStatusDto>($"/api/trips/{tripId}/status");
        Assert.NotNull(status);
        Assert.Equal("InProgress", status.Status);
        Assert.True(status.DrivenDistanceMeters > 0);
        Assert.True(status.CurrentPrice > 0);
        Assert.NotEmpty(status.Driven);
        Assert.NotEmpty(status.Remaining);

        await DriveSimulationAsync(ticks: 500);

        // Epic 3: eindprijs
        status = await client.GetFromJsonAsync<TripStatusDto>($"/api/trips/{tripId}/status");
        Assert.True(status!.IsFinished);
        var summaryHtml = await client.GetStringAsync($"/ritten/{tripId}/eindprijs");
        Assert.Contains("Te betalen", summaryHtml);
        Assert.Contains("Eerlijke prijs", summaryHtml);
    }

    [Fact] // IT-03
    public async Task PlanTrip_WithoutAntiForgeryToken_IsRejected()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/ritten/plannen", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Form.CityId"] = "1",
            ["Form.PickupPlaceId"] = "1",
            ["Form.DestinationPlaceId"] = "2"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact] // IT-04
    public async Task PlanTrip_WithoutDestination_ShowsValidationError()
    {
        var client = CreateClient();
        var (cityId, pickupId, _) = await PlacesAsync("Londen", "Big Ben", "Tower of London");

        var response = await PostFormAsync(client, "/", "/ritten/plannen", new()
        {
            ["Form.CityId"] = cityId.ToString(),
            ["Form.PickupPlaceId"] = pickupId.ToString()
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kies een bestemming uit de lijst of klik op de kaart.", html);
    }

    [Fact] // IT-05
    public async Task PlanTrip_WithMapCoordinates_UsesInvariantDecimalPoint()
    {
        var client = CreateClient();
        var (cityId, _, destinationId) = await PlacesAsync("Londen", "Big Ben", "Tower of London");

        var response = await PostFormAsync(client, "/", "/ritten/plannen", new()
        {
            ["Form.CityId"] = cityId.ToString(),
            ["Form.PickupLat"] = "51.503300",
            ["Form.PickupLng"] = "-0.119600",
            ["Form.DestinationPlaceId"] = destinationId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Punt op de kaart (51.5033, -0.1196)", html);
    }

    [Fact] // IT-06
    public async Task EndTripEarly_ShowsSummaryWithEndedEarlyNotice()
    {
        var client = CreateClient();
        var (cityId, pickupId, destinationId) = await PlacesAsync("Parijs", "Tour Eiffel", "Musée du Louvre");
        var estimateUrl = await PlanTripAsync(client, cityId, pickupId, destinationId);
        var tripId = TripIdFrom(estimateUrl);
        await PostFormAsync(client, estimateUrl, $"/ritten/{tripId}/start", new()
        {
            ["Form.TaxiId"] = (await AvailableTaxiAsync(cityId)).ToString()
        });
        await DriveSimulationAsync(ticks: 3);

        var end = await PostFormAsync(client, $"/ritten/{tripId}", $"/ritten/{tripId}/beeindigen", new());
        var summaryHtml = await client.GetStringAsync(end.Headers.Location);

        Assert.Equal($"/ritten/{tripId}/eindprijs", end.Headers.Location?.OriginalString);
        Assert.Contains("voortijdig beëindigd", summaryHtml);
    }

    [Fact] // IT-07
    public async Task PassengerMessage_IsHtmlEncoded()
    {
        var client = CreateClient();
        var (cityId, pickupId, destinationId) = await PlacesAsync("Londen", "London Eye", "British Museum");
        var estimateUrl = await PlanTripAsync(client, cityId, pickupId, destinationId);
        var tripId = TripIdFrom(estimateUrl);
        await PostFormAsync(client, estimateUrl, $"/ritten/{tripId}/start", new()
        {
            ["Form.TaxiId"] = (await AvailableTaxiAsync(cityId)).ToString()
        });

        await PostFormAsync(client, $"/ritten/{tripId}", $"/ritten/{tripId}/bericht", new()
        {
            ["Form.Message"] = "<script>alert('xss')</script>"
        });
        await PostFormAsync(client, $"/ritten/{tripId}", $"/ritten/{tripId}/beeindigen", new());
        var summaryHtml = await client.GetStringAsync($"/ritten/{tripId}/eindprijs");

        Assert.DoesNotContain("<script>alert('xss')</script>", summaryHtml);
        Assert.Contains("&lt;script&gt;", summaryHtml);
    }

    [Fact] // IT-08
    public async Task UnknownTrip_Returns404()
    {
        var client = CreateClient();

        var page = await client.GetAsync($"/ritten/{Guid.NewGuid()}");
        var api = await client.GetAsync($"/api/trips/{Guid.NewGuid()}/status");

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
    }

    [Fact] // IT-09
    public async Task TaxiApi_RequiresValidKeyOfTheAssignedTaxi()
    {
        var client = CreateClient();
        var (cityId, pickupId, destinationId) = await PlacesAsync("Amsterdam", "De Dam", "Artis");
        var estimateUrl = await PlanTripAsync(client, cityId, pickupId, destinationId);
        var tripId = TripIdFrom(estimateUrl);
        var taxiId = await AvailableTaxiAsync(cityId);
        await PostFormAsync(client, estimateUrl, $"/ritten/{tripId}/start", new() { ["Form.TaxiId"] = taxiId.ToString() });

        var plate = await WithDbAsync(db => db.Taxis.Where(t => t.Id == taxiId).Select(t => t.LicensePlate).SingleAsync());
        var otherPlate = await WithDbAsync(db => db.Taxis.Where(t => t.Id != taxiId).Select(t => t.LicensePlate).FirstAsync());
        var body = new PositionBatchRequest
        {
            Positions = [new PositionRequest { Lat = 52.3728, Lng = 4.8950, RecordedAt = DateTimeOffset.UtcNow.AddMinutes(1) }]
        };

        var noKey = await client.PostAsJsonAsync($"/api/taxi/trips/{tripId}/positions", body);
        var wrongTaxi = await SendWithKeyAsync(client, $"/api/taxi/trips/{tripId}/positions", DbSeeder.DemoApiKeyFor(otherPlate), body);
        var correct = await SendWithKeyAsync(client, $"/api/taxi/trips/{tripId}/positions", DbSeeder.DemoApiKeyFor(plate), body);

        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongTaxi.StatusCode);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        var status = await client.GetFromJsonAsync<TripStatusDto>($"/api/trips/{tripId}/status");
        Assert.True(status!.DrivenDistanceMeters > 0);

        var complete = await SendWithKeyAsync(client, $"/api/taxi/trips/{tripId}/complete", DbSeeder.DemoApiKeyFor(plate), null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    [Fact] // IT-10
    public async Task Responses_ContainSecurityHeaders()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/");

        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact] // IT-11
    public async Task Feedback_CanBeSubmittedOnceAfterTrip()
    {
        var client = CreateClient();
        var (cityId, pickupId, destinationId) = await PlacesAsync("Parijs", "Notre-Dame", "Gare du Nord");
        var estimateUrl = await PlanTripAsync(client, cityId, pickupId, destinationId);
        var tripId = TripIdFrom(estimateUrl);
        await PostFormAsync(client, estimateUrl, $"/ritten/{tripId}/start", new()
        {
            ["Form.TaxiId"] = (await AvailableTaxiAsync(cityId)).ToString()
        });
        await PostFormAsync(client, $"/ritten/{tripId}", $"/ritten/{tripId}/beeindigen", new());

        var feedback = await PostFormAsync(client, $"/ritten/{tripId}/eindprijs", $"/ritten/{tripId}/beoordeling", new()
        {
            ["Form.Stars"] = "2",
            ["Form.Comment"] = "Reed om",
            ["Form.ReportOvercharge"] = "true"
        });
        var summaryHtml = await client.GetStringAsync($"/ritten/{tripId}/eindprijs");

        Assert.Equal(HttpStatusCode.Redirect, feedback.StatusCode);
        Assert.Contains("Je gaf deze rit <strong>2 van de 5 sterren</strong>", summaryHtml);
        Assert.Contains("Je hebt gemeld dat je te veel betaald hebt", summaryHtml);
    }

    // ---------------- Hulpmethodes ----------------

    private async Task<string> PlanTripAsync(HttpClient client, int cityId, int pickupId, int destinationId)
    {
        var response = await PostFormAsync(client, $"/?cityId={cityId}", "/ritten/plannen", new()
        {
            ["Form.CityId"] = cityId.ToString(),
            ["Form.PickupPlaceId"] = pickupId.ToString(),
            ["Form.DestinationPlaceId"] = destinationId.ToString()
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }

    /// <summary>Haalt eerst de pagina met het formulier op (voor het anti-forgery token) en post dan.</summary>
    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string formPageUrl, string postUrl,
        Dictionary<string, string> fields)
    {
        var formPage = await client.GetStringAsync(formPageUrl);
        fields["__RequestVerificationToken"] = HitchTrackerWebFactory.AntiForgeryToken(formPage);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> SendWithKeyAsync(HttpClient client, string url, string key, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        request.Headers.Add(TaxiApiController.ApiKeyHeader, key);
        return client.SendAsync(request);
    }

    private static Guid TripIdFrom(string estimateUrl) => Guid.Parse(estimateUrl.Split('/')[2]);

    private async Task DriveSimulationAsync(int ticks)
    {
        using var scope = _factory.Services.CreateScope();
        var simulator = scope.ServiceProvider.GetRequiredService<TripSimulator>();
        for (var i = 0; i < ticks; i++)
        {
            if (await simulator.StepAllAsync(TimeSpan.FromSeconds(1)) == 0)
            {
                break;
            }
        }
    }

    private async Task<(int CityId, int PickupId, int DestinationId)> PlacesAsync(string city, string pickup, string destination) =>
        await WithDbAsync(async db =>
        {
            var cityId = await db.Cities.Where(c => c.Name == city).Select(c => c.Id).SingleAsync();
            var pickupId = await db.Places.Where(p => p.Name == pickup).Select(p => p.Id).SingleAsync();
            var destinationId = await db.Places.Where(p => p.Name == destination).Select(p => p.Id).SingleAsync();
            return (cityId, pickupId, destinationId);
        });

    private Task<int> AvailableTaxiAsync(int cityId) =>
        WithDbAsync(db => db.Taxis.Where(t => t.CityId == cityId && t.IsAvailable).Select(t => t.Id).FirstAsync());

    private async Task<T> WithDbAsync<T>(Func<HitchTrackerDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<HitchTrackerDbContext>());
    }
}

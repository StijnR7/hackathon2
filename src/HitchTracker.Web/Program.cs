using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using HitchTracker.Web.Data;
using HitchTracker.Web.Infrastructure;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Routing;
using HitchTracker.Web.Services.Simulation;
using HitchTracker.Web.Services.Trips;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(mvc => mvc.Filters.Add<TripExceptionFilter>());

// Tekens als € en ë niet als &#x20AC; in de HTML zetten; <, >, & en quotes worden nog steeds ge-encodeerd (XSS).
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Database (SQLite: geen aparte databaseserver nodig voor het MVP).
builder.Services.AddDbContext<HitchTrackerDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("HitchTracker")));

// Instellingen, gevalideerd bij het opstarten zodat een typefout in appsettings direct opvalt.
builder.Services.AddOptions<RoutingOptions>().BindConfiguration(RoutingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<TripMonitoringOptions>().BindConfiguration(TripMonitoringOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SimulationOptions>().BindConfiguration(SimulationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<DataRetentionOptions>().BindConfiguration(DataRetentionOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

// Routeplanner: OSRM (online) met offline terugvaloptie.
builder.Services.AddSingleton<OfflineRoutingService>();
builder.Services.AddHttpClient<OsrmRoutingService>((services, client) =>
{
    var routing = services.GetRequiredService<IOptions<RoutingOptions>>().Value;
    client.BaseAddress = new Uri(routing.OsrmBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(routing.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HitchTracker-MVP/1.0 (schoolproject)");
});
builder.Services.AddScoped<IRoutingService>(services =>
    services.GetRequiredService<IOptions<RoutingOptions>>().Value.Provider == RoutingProvider.Osrm
        ? services.GetRequiredService<OsrmRoutingService>()
        : services.GetRequiredService<OfflineRoutingService>());

builder.Services.AddSingleton<TripMonitor>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<TripSimulator>();
builder.Services.AddScoped<DataRetentionService>();
builder.Services.AddScoped<PlanTripViewModelFactory>();
builder.Services.AddHostedService<SimulationBackgroundService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();

// Beperk het aantal routeberekeningen per IP-adres (bescherming tegen misbruik van de externe routeplanner).
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(RateLimitPolicies.RoutePlanning, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "onbekend",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

// Invariante cultuur voor het inlezen van formulieren: coördinaten als "51.5074" moeten altijd goed gaan,
// ook op een server met Nederlandse landinstellingen. Weergave in het Nederlands gebeurt via Formatting.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");
app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

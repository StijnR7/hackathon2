using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace HitchTracker.Tests.Integration;

/// <summary>
/// Start de complete webapplicatie in het geheugen: echte middleware, controllers, views en database (SQLite),
/// maar met de offline routeplanner (geen internet nodig) en zonder achtergrondsimulator (de test bepaalt de tijd).
/// </summary>
public sealed partial class HitchTrackerWebFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source=file:hitchtracker-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;

    public HitchTrackerWebFactory()
    {
        // Een gedeelde in-memory database bestaat zolang er minstens één verbinding open is.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:HitchTracker", _connectionString);
        builder.UseSetting("Routing:Provider", "Offline");
        builder.UseSetting("Simulation:Enabled", "false");
        builder.UseSetting("DataRetention:Enabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }

    /// <summary>Haalt het anti-forgery token uit een HTML-formulier.</summary>
    public static string AntiForgeryToken(string html) =>
        AntiForgeryRegex().Match(html) is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Geen anti-forgery token gevonden in de pagina.");

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryRegex();
}

using HitchTracker.Web.Infrastructure;
using HitchTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Data;

/// <summary>
/// Vult een lege database met dummydata: steden, tarieven, bekende plaatsen, chauffeurs en taxi's.
/// LET OP: de tarieven zijn benaderingen voor de demo en niet de officiële tarieven van de steden.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// API-sleutels van de demo-taxi's. Alleen voor de demo; in productie krijgt elk taxikastje een eigen
    /// willekeurige sleutel die nergens in de broncode staat.
    /// </summary>
    public static string DemoApiKeyFor(string licensePlate) => $"demo-{licensePlate.Replace(" ", "-").ToLowerInvariant()}";

    public static async Task SeedAsync(HitchTrackerDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Cities.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Cities.AddRange(CreateLondon(), CreateAmsterdam(), CreateParis());
        await db.SaveChangesAsync(cancellationToken);
    }

    private static City CreateLondon()
    {
        var city = new City
        {
            Name = "Londen", Country = "Verenigd Koninkrijk", CurrencyCode = "GBP", TimeZoneId = "Europe/London",
            CenterLat = 51.5074, CenterLng = -0.1278, ServiceRadiusKm = 35
        };
        city.Tariffs.Add(CreateTariff("Dagtarief", 5, 20, baseFare: 3.80m, perKm: 2.20m, perMinute: 0.55m, minimum: 3.80m));
        city.Tariffs.Add(CreateTariff("Avond- en nachttarief", 20, 5, baseFare: 3.80m, perKm: 2.70m, perMinute: 0.65m, minimum: 3.80m));
        AddPlaces(city,
            ("Big Ben", "Bezienswaardigheid", 51.50073, -0.12463),
            ("Tower of London", "Bezienswaardigheid", 51.50812, -0.07595),
            ("British Museum", "Museum", 51.51939, -0.12700),
            ("Buckingham Palace", "Bezienswaardigheid", 51.50136, -0.14189),
            ("London Eye", "Bezienswaardigheid", 51.50329, -0.11955),
            ("King's Cross St Pancras", "Station", 51.53085, -0.12385),
            ("Paddington Station", "Station", 51.51538, -0.17548),
            ("Camden Market", "Winkelen", 51.54134, -0.14603),
            ("Natural History Museum", "Museum", 51.49671, -0.17636),
            ("Heathrow Airport (T5)", "Luchthaven", 51.47234, -0.48796));
        AddTaxis(city,
            ("LX19 TXI", "Zwarte LEVC TX (black cab)", "James", "PCO-482913", 4.8),
            ("LT68 CAB", "Zwarte TX4 (black cab)", "Amira", "PCO-517204", 4.9),
            ("LD21 HCK", "Zilveren Toyota Prius+", "Tom", "PCO-390876", 4.6));
        return city;
    }

    private static City CreateAmsterdam()
    {
        var city = new City
        {
            Name = "Amsterdam", Country = "Nederland", CurrencyCode = "EUR", TimeZoneId = "Europe/Amsterdam",
            CenterLat = 52.3676, CenterLng = 4.9041, ServiceRadiusKm = 25
        };
        city.Tariffs.Add(CreateTariff("Maximumtarief", 0, 0, baseFare: 3.40m, perKm: 2.50m, perMinute: 0.41m, minimum: 0m));
        AddPlaces(city,
            ("Amsterdam Centraal", "Station", 52.37903, 4.90005),
            ("De Dam", "Bezienswaardigheid", 52.37309, 4.89275),
            ("Rijksmuseum", "Museum", 52.35999, 4.88524),
            ("Anne Frank Huis", "Museum", 52.37518, 4.88397),
            ("Vondelpark", "Park", 52.35800, 4.86860),
            ("NEMO Science Museum", "Museum", 52.37414, 4.91232),
            ("Artis", "Dierentuin", 52.36603, 4.91664),
            ("Johan Cruijff ArenA", "Stadion", 52.31427, 4.94188),
            ("Schiphol Plaza", "Luchthaven", 52.30943, 4.76216));
        AddTaxis(city,
            ("TX-123-A", "Zwarte Tesla Model Y", "Mohamed", "TCA-200341", 4.7),
            ("TX-456-B", "Witte Toyota Corolla", "Sanne", "TCA-198765", 4.9),
            ("TX-789-C", "Blauwe Mercedes E-klasse", "Kees", "TCA-176502", 4.5));
        return city;
    }

    private static City CreateParis()
    {
        var city = new City
        {
            Name = "Parijs", Country = "Frankrijk", CurrencyCode = "EUR", TimeZoneId = "Europe/Paris",
            CenterLat = 48.8566, CenterLng = 2.3522, ServiceRadiusKm = 35
        };
        city.Tariffs.Add(CreateTariff("Tarif B (dag)", 7, 19, baseFare: 4.40m, perKm: 1.30m, perMinute: 0.60m, minimum: 7.30m));
        city.Tariffs.Add(CreateTariff("Tarif C (nacht)", 19, 7, baseFare: 4.40m, perKm: 1.65m, perMinute: 0.70m, minimum: 7.30m));
        AddPlaces(city,
            ("Tour Eiffel", "Bezienswaardigheid", 48.85837, 2.29448),
            ("Musée du Louvre", "Museum", 48.86061, 2.33764),
            ("Notre-Dame", "Bezienswaardigheid", 48.85296, 2.34990),
            ("Arc de Triomphe", "Bezienswaardigheid", 48.87380, 2.29504),
            ("Sacré-Cœur", "Bezienswaardigheid", 48.88671, 2.34310),
            ("Gare du Nord", "Station", 48.88094, 2.35533),
            ("Musée d'Orsay", "Museum", 48.86000, 2.32656),
            ("Aéroport Charles de Gaulle (T2)", "Luchthaven", 49.00406, 2.57108));
        AddTaxis(city,
            ("GT-512-PA", "Zwarte Peugeot 508", "Julien", "VTC-750113", 4.7),
            ("FR-224-TX", "Grijze Skoda Superb", "Léa", "VTC-750872", 4.8),
            ("EV-901-PR", "Witte Renault Mégane E-Tech", "Karim", "VTC-751404", 4.4));
        return city;
    }

    private static Tariff CreateTariff(string name, int startHour, int endHour,
        decimal baseFare, decimal perKm, decimal perMinute, decimal minimum) => new()
    {
        Name = name,
        StartHour = startHour,
        EndHour = endHour,
        Rates = new FareRates { BaseFare = baseFare, PricePerKm = perKm, PricePerMinute = perMinute, MinimumFare = minimum }
    };

    private static void AddPlaces(City city, params (string Name, string Category, double Lat, double Lng)[] places)
    {
        foreach (var (name, category, lat, lng) in places)
        {
            city.Places.Add(new Place { Name = name, Category = category, Lat = lat, Lng = lng });
        }
    }

    private static void AddTaxis(City city,
        params (string Plate, string Vehicle, string DriverName, string License, double Rating)[] taxis)
    {
        foreach (var (plate, vehicle, driverName, license, rating) in taxis)
        {
            city.Taxis.Add(new Taxi
            {
                LicensePlate = plate,
                VehicleDescription = vehicle,
                ApiKeyHash = ApiKeyHasher.Hash(DemoApiKeyFor(plate)),
                Driver = new Driver { FirstName = driverName, LicenseNumber = license, Rating = rating }
            });
        }
    }
}

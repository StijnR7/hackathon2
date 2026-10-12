using HitchTracker.Web.Models;
using HitchTracker.Web.Services;
using HitchTracker.Web.Services.Trips;

namespace HitchTracker.Tests.TestSupport;

/// <summary>Hulpmethodes om een rit in een bepaalde fase klaar te zetten.</summary>
public static class TripScenario
{
    /// <summary>Amsterdam Centraal naar het Rijksmuseum (ca. 2,4 km hemelsbreed).</summary>
    public static async Task<Trip> PlanAmsterdamTripAsync(TestDatabase database, TripService trips)
    {
        var city = database.City("Amsterdam");
        return await trips.PlanTripAsync(city.Id,
            new LocationInput(database.PlaceId("Amsterdam Centraal"), null, null),
            new LocationInput(database.PlaceId("Rijksmuseum"), null, null));
    }

    public static async Task<Trip> StartAmsterdamTripAsync(TestDatabase database, TripService trips)
    {
        var planned = await PlanAmsterdamTripAsync(database, trips);
        return await trips.StartTripAsync(planned.Id, database.AvailableTaxiId(planned.CityId));
    }

    /// <summary>Rijdt de taxi exact over de geplande route tot <paramref name="fraction"/> van de afstand.</summary>
    public static List<PositionReport> DriveAlongPlannedRoute(Trip trip, double fraction)
    {
        var speed = trip.PlannedDistanceMeters / trip.PlannedDurationSeconds;
        var target = trip.PlannedDistanceMeters * fraction;
        var points = GeoMath.SubPath(trip.PlannedRoute, 0, target);

        var reports = new List<PositionReport>();
        var along = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            along += GeoMath.Distance(points[i - 1], points[i]);
            reports.Add(new PositionReport(points[i], trip.StartedAt!.Value.AddSeconds(along / speed)));
        }

        return reports;
    }
}

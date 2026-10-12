namespace HitchTracker.Web.Models;

/// <summary>
/// Demo-hulpmiddel: omdat er in het MVP geen echte taxi's rijden, "rijdt" de simulator de taxi over dit pad
/// en levert hij GPS-posities aan via dezelfde code die een echt taxikastje zou gebruiken.
/// </summary>
public class TripSimulation
{
    public Guid TripId { get; set; }
    public List<GeoPoint> Path { get; set; } = [];
    public double PathLengthMeters { get; set; }
    public double DistanceTravelledMeters { get; set; }
    public double SpeedMetersPerSecond { get; set; }

    /// <summary>True als de gesimuleerde chauffeur bewust omrijdt.</summary>
    public bool WithDetour { get; set; }
}

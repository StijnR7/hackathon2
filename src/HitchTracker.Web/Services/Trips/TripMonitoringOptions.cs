using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Services.Trips;

public class TripMonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Vanaf deze afstand tot de geplande route krijgt de reiziger een waarschuwing.</summary>
    [Range(10, 5000)]
    public double OffRouteThresholdMeters { get; set; } = 100;

    /// <summary>
    /// Binnen deze afstand geldt de taxi weer als "op route". Lager dan de drempel hierboven (hysterese),
    /// zodat GPS-ruis rond de grens niet steeds nieuwe meldingen geeft.
    /// </summary>
    [Range(5, 5000)]
    public double BackOnRouteThresholdMeters { get; set; } = 50;

    /// <summary>De bovengrens van de schatting gaat uit van zoveel keer de normale rijtijd (druk verkeer).</summary>
    [Range(1.0, 5.0)]
    public double TrafficMarginFactor { get; set; } = 1.4;

    /// <summary>Minimale tijd tussen twee herberekeningen van de resterende route als de taxi is afgeweken.</summary>
    [Range(1, 600)]
    public int RerouteIntervalSeconds { get; set; } = 15;

    /// <summary>Snellere verplaatsingen worden als GPS-fout genegeerd (km/u).</summary>
    [Range(50, 400)]
    public double MaxPlausibleSpeedKmh { get; set; } = 200;

    /// <summary>Minimale afstand tussen ophaallocatie en bestemming.</summary>
    [Range(0, 5000)]
    public double MinimumTripDistanceMeters { get; set; } = 200;
}

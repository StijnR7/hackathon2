using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Services.Simulation;

public class SimulationOptions
{
    public const string SectionName = "Simulation";

    /// <summary>Zet de achtergrondsimulator aan of uit (uit in de geautomatiseerde tests).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Hoe vaak de gesimuleerde taxi een nieuwe positie doorgeeft.</summary>
    [Range(100, 60_000)]
    public int TickMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Versnelling van de simulatie: bij 10 duurt een rit van 10 minuten in de demo 1 minuut.
    /// De meter rekent wel met de gesimuleerde (echte) ritduur.
    /// </summary>
    [Range(1, 100)]
    public double SpeedFactor { get; set; } = 10;
}

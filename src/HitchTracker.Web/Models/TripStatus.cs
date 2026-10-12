namespace HitchTracker.Web.Models;

public enum TripStatus
{
    /// <summary>Prijsschatting gemaakt, nog niet ingestapt.</summary>
    Planned,

    /// <summary>De reiziger zit in de taxi, de meter loopt.</summary>
    InProgress,

    /// <summary>Bestemming bereikt.</summary>
    Completed,

    /// <summary>De reiziger heeft de rit voortijdig beëindigd.</summary>
    EndedEarly
}

public static class TripStatusExtensions
{
    public static string DisplayName(this TripStatus status) => status switch
    {
        TripStatus.Planned => "Gepland",
        TripStatus.InProgress => "Onderweg",
        TripStatus.Completed => "Aangekomen",
        TripStatus.EndedEarly => "Voortijdig beëindigd",
        _ => status.ToString()
    };
}

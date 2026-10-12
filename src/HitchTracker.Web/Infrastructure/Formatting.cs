using System.Globalization;

namespace HitchTracker.Web.Infrastructure;

/// <summary>
/// Nederlandse weergave van bedragen, afstanden en tijden.
/// De applicatie draait zelf met de invariante cultuur (zodat coördinaten met een punt als decimaalteken
/// altijd correct worden ingelezen); alleen voor weergave gebruiken we nl-NL.
/// </summary>
public static class Formatting
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    public static string CurrencySymbol(string currencyCode) => currencyCode switch
    {
        "EUR" => "€",
        "GBP" => "£",
        "USD" => "$",
        _ => currencyCode
    };

    public static string Money(decimal amount, string currencyCode) =>
        $"{CurrencySymbol(currencyCode)} {amount.ToString("N2", Dutch)}";

    public static string Kilometers(double meters) =>
        $"{(meters / 1000).ToString("0.0", Dutch)} km";

    /// <summary>Nauwkeurig genoeg om een prijsopbouw na te rekenen (bijv. "5,52 km").</summary>
    public static string PreciseKilometers(double meters) =>
        $"{(meters / 1000).ToString("0.00", Dutch)} km";

    /// <summary>Nauwkeurig genoeg om een prijsopbouw na te rekenen (bijv. "12,5 min").</summary>
    public static string PreciseMinutes(double seconds) =>
        $"{(seconds / 60).ToString("0.0", Dutch)} min";

    public static string Minutes(double seconds)
    {
        var minutes = (int)Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
        return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} u {minutes % 60:00} min";
    }

    public static string Time(DateTime localTime) => localTime.ToString("HH:mm", Dutch);

    public static string DateTime(DateTime localTime) => localTime.ToString("d MMMM yyyy, HH:mm", Dutch);

    public static string Number(double value, string format = "0.0") => value.ToString(format, Dutch);
}

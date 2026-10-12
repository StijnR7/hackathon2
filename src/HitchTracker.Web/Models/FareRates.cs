using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Models;

/// <summary>
/// De tariefbedragen waarmee een ritprijs berekend wordt.
/// Wordt als "owned type" opgeslagen bij zowel een <see cref="Tariff"/> als een <see cref="Trip"/>:
/// een rit bewaart een kopie, zodat een latere tariefwijziging de prijs van een lopende rit niet verandert.
/// </summary>
[Owned]
public class FareRates
{
    public decimal BaseFare { get; set; }
    public decimal PricePerKm { get; set; }
    public decimal PricePerMinute { get; set; }
    public decimal MinimumFare { get; set; }

    /// <summary>
    /// Berekent de ritprijs voor een afstand en een duur.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Bij een negatieve afstand of duur.</exception>
    public FareBreakdown Calculate(double distanceMeters, double durationSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);

        var kilometers = (decimal)distanceMeters / 1000m;
        var minutes = (decimal)durationSeconds / 60m;

        var distanceCost = RoundMoney(kilometers * PricePerKm);
        var timeCost = RoundMoney(minutes * PricePerMinute);
        var subtotal = BaseFare + distanceCost + timeCost;
        var minimumSurcharge = subtotal < MinimumFare ? MinimumFare - subtotal : 0m;

        return new FareBreakdown(BaseFare, distanceCost, timeCost, minimumSurcharge);
    }

    public FareRates Copy() => new()
    {
        BaseFare = BaseFare,
        PricePerKm = PricePerKm,
        PricePerMinute = PricePerMinute,
        MinimumFare = MinimumFare
    };

    private static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Opbouw van een ritprijs, zodat de gebruiker kan zien waar het bedrag vandaan komt.</summary>
public sealed record FareBreakdown(
    decimal BaseFare,
    decimal DistanceCost,
    decimal TimeCost,
    decimal MinimumFareSurcharge)
{
    public decimal Total => BaseFare + DistanceCost + TimeCost + MinimumFareSurcharge;
}

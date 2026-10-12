using HitchTracker.Web.Models;
using HitchTracker.Web.Services.Trips;

namespace HitchTracker.Tests.Unit;

public class PricingTests
{
    private static FareRates LondonDayRates() => new()
    {
        BaseFare = 3.80m,
        PricePerKm = 2.20m,
        PricePerMinute = 0.55m,
        MinimumFare = 3.80m
    };

    [Fact] // UT-11
    public void Calculate_AddsBaseDistanceAndTime()
    {
        // 5 km en 12 minuten: 3,80 + 5 x 2,20 + 12 x 0,55 = 3,80 + 11,00 + 6,60 = 21,40
        var fare = LondonDayRates().Calculate(5_000, 12 * 60);

        Assert.Equal(3.80m, fare.BaseFare);
        Assert.Equal(11.00m, fare.DistanceCost);
        Assert.Equal(6.60m, fare.TimeCost);
        Assert.Equal(21.40m, fare.Total);
    }

    [Fact] // UT-12
    public void Calculate_BelowMinimumFare_AddsSurcharge()
    {
        var rates = new FareRates { BaseFare = 4.40m, PricePerKm = 1.30m, PricePerMinute = 0.60m, MinimumFare = 7.30m };

        var fare = rates.Calculate(500, 60); // 4,40 + 0,65 + 0,60 = 5,65

        Assert.Equal(1.65m, fare.MinimumFareSurcharge);
        Assert.Equal(7.30m, fare.Total);
    }

    [Fact] // UT-13
    public void Calculate_RoundsToCents()
    {
        var fare = LondonDayRates().Calculate(1_234, 77); // 1,234 km x 2,20 = 2,7148; 1,2833 min x 0,55 = 0,7058

        Assert.Equal(2.71m, fare.DistanceCost);
        Assert.Equal(0.71m, fare.TimeCost);
    }

    [Theory] // UT-14
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Calculate_NegativeInput_Throws(double meters, double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LondonDayRates().Calculate(meters, seconds));
    }

    [Fact] // UT-15
    public void Copy_IsIndependentOfOriginal()
    {
        var original = LondonDayRates();
        var copy = original.Copy();

        original.PricePerKm = 99m;

        Assert.Equal(2.20m, copy.PricePerKm);
    }

    [Theory] // UT-16
    [InlineData(5, 20, 4, false)]
    [InlineData(5, 20, 5, true)]
    [InlineData(5, 20, 19, true)]
    [InlineData(5, 20, 20, false)]
    [InlineData(20, 5, 23, true)]  // nachttarief over middernacht
    [InlineData(20, 5, 2, true)]
    [InlineData(20, 5, 12, false)]
    [InlineData(0, 0, 15, true)]   // hele dag
    public void Tariff_AppliesAt_HandlesPeriodsAcrossMidnight(int start, int end, int hour, bool expected)
    {
        var tariff = new Tariff { StartHour = start, EndHour = end };

        Assert.Equal(expected, tariff.AppliesAt(hour));
    }

    [Theory] // UT-17
    [InlineData(12, "Dagtarief")]                 // 12:00 UTC = 13:00 in Londen (zomertijd)
    [InlineData(21, "Avond- en nachttarief")]     // 21:00 UTC = 22:00 in Londen
    [InlineData(3, "Avond- en nachttarief")]      // 03:00 UTC = 04:00 in Londen
    public void TariffSelector_UsesLocalTimeOfCity(int utcHour, string expectedTariff)
    {
        var london = new City { TimeZoneId = "Europe/London" };
        london.Tariffs.Add(new Tariff { Id = 1, Name = "Dagtarief", StartHour = 5, EndHour = 20 });
        london.Tariffs.Add(new Tariff { Id = 2, Name = "Avond- en nachttarief", StartHour = 20, EndHour = 5 });

        var tariff = TariffSelector.Select(london, new DateTime(2026, 7, 1, utcHour, 0, 0, DateTimeKind.Utc));

        Assert.Equal(expectedTariff, tariff?.Name);
    }

    [Fact] // UT-18
    public void TariffSelector_PrefersSpecificTariffOverAllDay()
    {
        var city = new City { TimeZoneId = "Europe/Amsterdam" };
        city.Tariffs.Add(new Tariff { Id = 1, Name = "Hele dag", StartHour = 0, EndHour = 0 });
        city.Tariffs.Add(new Tariff { Id = 2, Name = "Nacht", StartHour = 22, EndHour = 6 });

        var tariff = TariffSelector.Select(city, new DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Nacht", tariff?.Name);
    }
}

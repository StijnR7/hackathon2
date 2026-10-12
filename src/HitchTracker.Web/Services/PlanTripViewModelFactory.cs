using HitchTracker.Web.Data;
using HitchTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Services;

/// <summary>Bouwt het model voor de pagina "Rit plannen" (gebruikt door Home én Trips bij een validatiefout).</summary>
public class PlanTripViewModelFactory(HitchTrackerDbContext db)
{
    public async Task<PlanTripViewModel?> CreateAsync(int? cityId, EstimateForm? form = null,
        CancellationToken cancellationToken = default)
    {
        var cities = await db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        // Standaard de eerst aangemaakte stad (Londen, het voorbeeld uit de casus).
        var city = cities.FirstOrDefault(c => c.Id == (form?.CityId ?? cityId)) ?? cities.MinBy(c => c.Id);
        if (city is null)
        {
            return null;
        }

        var places = await db.Places.AsNoTracking()
            .Where(p => p.CityId == city.Id)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new PlanTripViewModel
        {
            City = city,
            Cities = cities.Select(c => new SelectListItem($"{c.Name} ({c.Country})", c.Id.ToString(), c.Id == city.Id)).ToList(),
            Places = places,
            Form = form ?? new EstimateForm { CityId = city.Id }
        };
    }
}

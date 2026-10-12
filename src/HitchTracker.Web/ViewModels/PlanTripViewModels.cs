using System.ComponentModel.DataAnnotations;
using HitchTracker.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HitchTracker.Web.ViewModels;

/// <summary>Formulier "Waar wil je heen?". Een locatie is óf een bekende plaats óf een punt op de kaart.</summary>
public class EstimateForm : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Kies een stad.")]
    public int CityId { get; set; }

    [Display(Name = "Ophaallocatie")]
    public int? PickupPlaceId { get; set; }

    [Range(-90, 90)]
    public double? PickupLat { get; set; }

    [Range(-180, 180)]
    public double? PickupLng { get; set; }

    [Display(Name = "Bestemming")]
    public int? DestinationPlaceId { get; set; }

    [Range(-90, 90)]
    public double? DestinationLat { get; set; }

    [Range(-180, 180)]
    public double? DestinationLng { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PickupPlaceId is null && (PickupLat is null || PickupLng is null))
        {
            yield return new ValidationResult("Kies een ophaallocatie uit de lijst of klik op de kaart.", [nameof(PickupPlaceId)]);
        }

        if (DestinationPlaceId is null && (DestinationLat is null || DestinationLng is null))
        {
            yield return new ValidationResult("Kies een bestemming uit de lijst of klik op de kaart.", [nameof(DestinationPlaceId)]);
        }

        if (PickupPlaceId is not null && PickupPlaceId == DestinationPlaceId)
        {
            yield return new ValidationResult("De bestemming moet een andere plek zijn dan de ophaallocatie.", [nameof(DestinationPlaceId)]);
        }
    }
}

public class PlanTripViewModel
{
    public required City City { get; init; }
    public required IReadOnlyList<SelectListItem> Cities { get; init; }
    public required IReadOnlyList<Place> Places { get; init; }
    public required EstimateForm Form { get; init; }

    public IEnumerable<SelectListItem> PlaceOptions(int? selectedId) =>
        Places.Select(p => new SelectListItem($"{p.Name} ({p.Category.ToLowerInvariant()})", p.Id.ToString(), p.Id == selectedId));

    /// <summary>Gegevens voor de kaart (plan.js).</summary>
    public object MapData => new
    {
        center = City.Center.ToArray(),
        places = Places.Select(p => new { id = p.Id, name = p.Name, position = p.Location.ToArray() })
    };
}

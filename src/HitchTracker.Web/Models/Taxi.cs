using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>Een taxi (voertuig) met de chauffeur die er op dit moment in rijdt.</summary>
public class Taxi
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public City City { get; set; } = null!;
    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    [MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [MaxLength(80)]
    public string VehicleDescription { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// SHA-256-hash van de API-sleutel waarmee het taxikastje/de chauffeursapp GPS-posities aanlevert.
    /// De sleutel zelf wordt nooit opgeslagen.
    /// </summary>
    [MaxLength(64)]
    public string ApiKeyHash { get; set; } = string.Empty;
}

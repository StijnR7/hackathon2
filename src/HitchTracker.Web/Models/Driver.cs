using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>
/// Een taxichauffeur. Bewust minimaal: alleen wat de reiziger nodig heeft om de chauffeur te herkennen
/// en zo nodig te melden (voornaam + vergunningnummer). Geen achternaam, adres of telefoonnummer.
/// </summary>
public class Driver
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Nummer van de taxivergunning, bijv. de PCO-licentie in Londen.</summary>
    [MaxLength(30)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Range(1, 5)]
    public double Rating { get; set; }
}

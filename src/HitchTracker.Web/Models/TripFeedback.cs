using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>Beoordeling van de rit door de reiziger, met de mogelijkheid om te veel betalen te melden.</summary>
public class TripFeedback
{
    public int Id { get; set; }
    public Guid TripId { get; set; }

    [Range(1, 5)]
    public int Stars { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public bool ReportedOvercharge { get; set; }

    public DateTime CreatedAt { get; set; }
}

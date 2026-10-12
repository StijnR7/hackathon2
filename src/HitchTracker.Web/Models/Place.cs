using System.ComponentModel.DataAnnotations;

namespace HitchTracker.Web.Models;

/// <summary>Een bekende locatie (bezienswaardigheid, station, luchthaven) die de reiziger kan kiezen.</summary>
public class Place
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public City City { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    public double Lat { get; set; }
    public double Lng { get; set; }

    public GeoPoint Location => new(Lat, Lng);
}

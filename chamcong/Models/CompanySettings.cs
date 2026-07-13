using System.ComponentModel.DataAnnotations;

namespace chamcong.Models;

public class CompanySettings
{
    [Key]
    public int Id { get; set; }
    public string? CompanyLocationUrl { get; set; }
    public double? CompanyLat { get; set; }
    public double? CompanyLng { get; set; }
    public double? LocationAcceptThresholdMeters { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
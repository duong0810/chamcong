namespace chamcong.DTOs;

public class CompanySettingsDto
{
    public int Id { get; set; }
    public string? CompanyLocationUrl { get; set; }
    public double? CompanyLat { get; set; }
    public double? CompanyLng { get; set; }
    public double? LocationAcceptThresholdMeters { get; set; }
}
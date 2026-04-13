using System.ComponentModel.DataAnnotations;

namespace chamcong.Models;

public class AttendanceRecord
{
    [Key]
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    public DateTime CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Accuracy { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public double? CheckOutAccuracy { get; set; }

    [MaxLength(500)]
    public string? CheckInDeviceInfo { get; set; }

    [MaxLength(500)]
    public string? CheckOutDeviceInfo { get; set; }

    public double? FaceMatchScore { get; set; }


    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsManualEntry { get; set; }
    public double? CheckInDistanceMeters { get; set; }
    public double? CheckOutDistanceMeters { get; set; }

    [MaxLength(45)]
    public string? CheckInIp { get; set; }

    [MaxLength(45)]
    public string? CheckOutIp { get; set; }

    [MaxLength(500)]
    public string? CheckInFaceImageUrl { get; set; }

    [MaxLength(500)]
    public string? CheckOutFaceImageUrl { get; set; }
 
    public Employee? Employee { get; set; }
}
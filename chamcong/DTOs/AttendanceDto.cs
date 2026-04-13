using chamcong.Models;

namespace chamcong.DTOs;

public class AttendanceDto
{
    public int EmployeeId { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Accuracy { get; set; }
    public double FaceMatchScore { get; set; }
    public bool IsManualEntry { get; set; } 
    public double? CheckInDistanceMeters { get; set; }
    public double? CheckOutDistanceMeters { get; set; }
    public string? CheckInFaceImageBase64 { get; set; }
    public string? CheckOutFaceImageBase64 { get; set; }
    public string? CheckInIp { get; set; }
    public string? CheckOutIp { get; set; }
    public string? CheckInDeviceInfo { get; set; }
    public string? CheckOutDeviceInfo { get; set; }
}

public class AttendanceHistoryDto
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public double? FaceMatchScore { get; set; }
    public string? CheckInFaceImageUrl { get; set; }
    public string? CheckOutFaceImageUrl { get; set; }
    public double? CheckInDistanceMeters { get; set; }
    public double? CheckOutDistanceMeters { get; set; }
    public string? CheckInIp { get; set; }
    public string? CheckOutIp { get; set; }
    public string? CheckInDeviceInfo { get; set; }
    public string? CheckOutDeviceInfo { get; set; }
    public string? Notes { get; set; }

}

public class AttendanceResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AttendanceRecord? Record { get; set; }
}

public class NotesUpdateDto
{
    public string? Notes { get; set; }
}


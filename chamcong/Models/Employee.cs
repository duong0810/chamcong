using System.ComponentModel.DataAnnotations;

namespace chamcong.Models;

public class Employee
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? Position { get; set; }

    // Face Recognition Data (JSON serialized array of numbers)
    public string? FaceDescriptor { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
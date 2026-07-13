using chamcong.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class AttendanceEditLog
{
    [Key]
    public int Id { get; set; }

    public int AttendanceRecordId { get; set; }

    public int EditedByUserId { get; set; }

    [MaxLength(50)]
    public string EditedByUsername { get; set; } = "";

    public DateTime EditedAt { get; set; } = DateTime.Now;

    public DateTime OldCheckInTime { get; set; }
    public DateTime? OldCheckOutTime { get; set; }

    public DateTime NewCheckInTime { get; set; }
    public DateTime? NewCheckOutTime { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public int EditCount { get; set; } = 1;

    // Navigation — bỏ ? để tránh CS0246 / CS0452 với EF Core generic constraint
    [ForeignKey(nameof(AttendanceRecordId))]
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    [ForeignKey(nameof(EditedByUserId))]
    public AppUser EditedByUser { get; set; } = null!;
}
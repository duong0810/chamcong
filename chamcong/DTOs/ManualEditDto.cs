public class ManualEditDto
{
    public DateTime NewCheckInTime { get; set; }
    public DateTime? NewCheckOutTime { get; set; }
    public string? Reason { get; set; }
    public int EditedByUserId { get; set; }
    public string EditedByUsername { get; set; } = "";
}

public class AttendanceEditLogDto
{
    public int Id { get; set; }
    public string EditedByUsername { get; set; } = "";
    public DateTime EditedAt { get; set; }
    public DateTime OldCheckInTime { get; set; }
    public DateTime? OldCheckOutTime { get; set; }
    public DateTime NewCheckInTime { get; set; }
    public DateTime? NewCheckOutTime { get; set; }
    public string? Reason { get; set; }
    public int EditCount { get; set; }
}
namespace chamcong.Models;

public class LoginLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime LoginAt { get; set; }
    public DateTime? LogoutAt { get; set; }
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";

    public AppUser? User { get; set; }
}
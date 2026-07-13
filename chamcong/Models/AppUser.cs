namespace chamcong.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";              // Admin | Manager | User
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastLogoutAt { get; set; }
    public int TokenVersion { get; set; } = 0;

    // Liên kết chức danh nhân viên
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}
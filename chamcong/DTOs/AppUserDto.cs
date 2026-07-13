namespace chamcong.DTOs;

public class AppUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastLogoutAt { get; set; }
    public List<string>? Permissions { get; set; } 
    public int? EmployeeId { get; set; }

}

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public int? EmployeeId { get; set; }
}

public class ChangeRoleDto
{
    public string Role { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    public string NewPassword { get; set; } = string.Empty;
}
namespace chamcong.DTOs;

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LogoutDto
{
    public int UserId { get; set; }
}
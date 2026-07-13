using chamcong.Data;
using chamcong.DTOs;
using chamcong.Models;
using chamcong.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace chamcong.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    // 🔒 Tài khoản hệ thống cố định, không được phép thao tác
    private const string PROTECTED_USERNAME = "admin";

    public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/account
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.AppUsers
            .OrderBy(u => u.Username)
            .Select(u => new AppUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                EmployeeId = u.EmployeeId
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST: api/account
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { message = "Tên đăng nhập và mật khẩu không được để trống." });

        // 🔒 Không cho phép tạo lại tài khoản hệ thống
        if (dto.Username.Trim().ToLower() == PROTECTED_USERNAME)
            return BadRequest(new { message = $"Tên đăng nhập '{dto.Username}' là tài khoản hệ thống, không thể tạo." });

        var exists = await _context.AppUsers.AnyAsync(u => u.Username == dto.Username);
        if (exists)
            return BadRequest(new { message = $"Tên đăng nhập '{dto.Username}' đã tồn tại." });

        var user = new AppUser
        {
            Username = dto.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FullName = dto.FullName?.Trim() ?? dto.Username,
            Role = dto.Role ?? "User",
            IsActive = true,
            CreatedAt = DateTime.Now,
            EmployeeId = dto.EmployeeId
        };

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("✅ Tạo tài khoản '{Username}' role '{Role}'", user.Username, user.Role);
        return Ok(new AppUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            EmployeeId = user.EmployeeId
        });
    }

    // PUT: api/account/{id}/role
    [HttpPut("{id}/role")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto dto)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        // 🔒 Bảo vệ tài khoản hệ thống
        if (user.Username.ToLower() == PROTECTED_USERNAME)
            return BadRequest(new { message = "Không thể thay đổi quyền tài khoản hệ thống." });

        var oldRole = user.Role;
        user.Role = dto.Role;
        await _context.SaveChangesAsync();

        AuthService.InvalidatePermissionCache(oldRole);
        AuthService.InvalidatePermissionCache(dto.Role);

        _logger.LogInformation("🎭 Đổi quyền user '{Username}': {OldRole} → {NewRole}", user.Username, oldRole, dto.Role);
        return Ok(new { message = $"Đã đổi quyền thành {dto.Role}" });
    }

    // PUT: api/account/{id}/password
    [HttpPut("{id}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 2)
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 2 ký tự." });

        var user = await _context.AppUsers.FindAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        // 🔒 Bảo vệ tài khoản hệ thống
        if (user.Username.ToLower() == PROTECTED_USERNAME)
            return BadRequest(new { message = "Không thể đặt lại mật khẩu tài khoản hệ thống." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã đặt lại mật khẩu thành công." });
    }

    // PUT: api/account/{id}/toggle
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        // 🔒 Bảo vệ tài khoản hệ thống
        if (user.Username.ToLower() == PROTECTED_USERNAME)
            return BadRequest(new { message = "Không thể vô hiệu hóa tài khoản hệ thống." });

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        var status = user.IsActive ? "kích hoạt" : "vô hiệu hóa";
        return Ok(new { message = $"Đã {status} tài khoản '{user.Username}'." });
    }

    // DELETE: api/account/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.AppUsers.FindAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        // 🔒 Bảo vệ tài khoản hệ thống
        if (user.Username.ToLower() == PROTECTED_USERNAME)
            return BadRequest(new { message = "Không thể xóa tài khoản hệ thống." });

        _context.AppUsers.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã xóa tài khoản '{user.Username}'." });
    }

    // POST: api/account/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == dto.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        // ✅ Kiểm tra nhân viên nghỉ việc TRƯỚC (ưu tiên thông báo này)
        if (user.EmployeeId.HasValue)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

            if (employee != null && !employee.IsActive)
                return Unauthorized(new { message = "Tài khoản bị khoá do nhân viên đã nghỉ việc." });
        }

        // ✅ Kiểm tra tài khoản bị vô hiệu hóa thủ công SAU
        if (!user.IsActive)
            return Unauthorized(new { message = "Tài khoản đã bị vô hiệu hóa." });

        user.LastLoginAt = DateTime.Now;

        // ✅ Upsert login log: mỗi user chỉ giữ 1 dòng duy nhất
        var existingLog = await _context.LoginLogs.FirstOrDefaultAsync(l => l.UserId == user.Id);
        if (existingLog != null)
        {
            existingLog.LoginAt = DateTime.Now;
            existingLog.LogoutAt = null;
            existingLog.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            existingLog.UserAgent = HttpContext.Request.Headers.UserAgent.ToString();
            existingLog.FullName = user.FullName;
            existingLog.Role = user.Role;
        }
        else
        {
            _context.LoginLogs.Add(new LoginLog
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                LoginAt = DateTime.Now,
                LogoutAt = null,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new AppUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            EmployeeId = user.EmployeeId
        });
    }

    // POST: api/account/{id}/logout
    [HttpPost("{id}/logout")]
    public async Task<IActionResult> Logout(int id)
    {
        var log = await _context.LoginLogs.FirstOrDefaultAsync(l => l.UserId == id);
        if (log != null)
        {
            log.LogoutAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Đã đăng xuất." });
    }

    // GET: api/account/login-history?days=30
    [HttpGet("login-history")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] int days = 30)
    {
        var from = DateTime.Now.AddDays(-days);
        var logs = await _context.LoginLogs
            .Where(l => l.LoginAt >= from)
            .OrderByDescending(l => l.LoginAt)
            .Select(l => new LoginLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                Username = l.Username,
                FullName = l.FullName,
                Role = l.Role,
                LoginAt = l.LoginAt,
                LogoutAt = l.LogoutAt,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent
            })
            .ToListAsync();

        return Ok(logs);
    }
}
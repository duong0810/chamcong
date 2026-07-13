using chamcong.Data;
using chamcong.DTOs;
using chamcong.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace chamcong.Services;

public class AuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ProtectedLocalStorage _localStorage; 

    private static readonly Dictionary<string, HashSet<string>> _permissionCache = new();
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    private AppUser? _currentUser;

    public event Func<Task>? OnAuthStateChanged;

    public AuthService(
        ILogger<AuthService> logger,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ProtectedLocalStorage localStorage) 
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _localStorage = localStorage; 
    }

    public AppUser? CurrentUser => _currentUser;

    public async Task<bool> TryRestoreSessionAsync()
    {
        if (_currentUser != null) return true;

        try
        {
            var result = await _localStorage.GetAsync<AppUserDto>("currentUser"); // ✅ Dùng _localStorage
            if (result.Success && result.Value != null)
            {
                _currentUser = new AppUser
                {
                    Id = result.Value.Id,
                    Username = result.Value.Username,
                    FullName = result.Value.FullName,
                    Role = result.Value.Role,
                    IsActive = result.Value.IsActive,
                    LastLoginAt = result.Value.LastLoginAt,
                    EmployeeId = result.Value.EmployeeId
                };
                _logger.LogInformation("🔄 [AuthService] Khôi phục session: {Username} ({Role})",
                    _currentUser.Username, _currentUser.Role);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ [AuthService] TryRestoreSessionAsync lỗi: {Message}", ex.Message);
        }

        return false;
    }

    public async Task LoginAsync(AppUserDto dto)
    {
        _currentUser = new AppUser
        {
            Id = dto.Id,
            Username = dto.Username,
            FullName = dto.FullName,
            Role = dto.Role,
            IsActive = dto.IsActive,
            LastLoginAt = dto.LastLoginAt,
            EmployeeId = dto.EmployeeId
        };

        await _localStorage.SetAsync("currentUser", dto); // ✅ Dùng _localStorage

        _logger.LogInformation("✅ [AuthService] LoginAsync: user={User} role={Role}",
            dto.Username, dto.Role);

        if (OnAuthStateChanged != null)
            await OnAuthStateChanged.Invoke();
    }

    public async Task LogoutAsync()
    {
        _logger.LogInformation("🚪 [AuthService] LogoutAsync → {Username}",
            _currentUser?.Username ?? "null");
        _currentUser = null;

        try { await _localStorage.DeleteAsync("currentUser"); } catch { } // ✅ Dùng _localStorage

        if (OnAuthStateChanged != null)
            await OnAuthStateChanged.Invoke();
    }

    // ===== Tất cả các method còn lại GIỮ NGUYÊN =====
    public void SetCurrentUser(AppUser? user) => _currentUser = user;
    public Task<bool> IsLoggedInAsync() => Task.FromResult(_currentUser != null && _currentUser.IsActive);
    public Task<bool> IsAuthenticatedAsync() => Task.FromResult(_currentUser != null && _currentUser.IsActive);
    public Task<bool> HasRoleAsync(string role)
    {
        var result = _currentUser != null &&
            string.Equals(_currentUser.Role, role, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(result);
    }
    public Task<bool> HasAnyRoleAsync(params string[] roles)
    {
        if (_currentUser == null) return Task.FromResult(false);
        var result = roles.Any(r =>
            string.Equals(_currentUser.Role, r, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }
    public async Task<bool> HasPermissionAsync(string permissionName)
    {
        if (_currentUser == null) return false;
        if (string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            var permissions = await GetPermissionsForRoleAsync(_currentUser.Role);
            return permissions.Contains(permissionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [AuthService] HasPermissionAsync lỗi");
            return false;
        }
    }
    public Task<AppUserDto?> GetCurrentUserAsync()
    {
        if (_currentUser == null) return Task.FromResult<AppUserDto?>(null);
        return Task.FromResult<AppUserDto?>(new AppUserDto
        {
            Id = _currentUser.Id,
            Username = _currentUser.Username,
            FullName = _currentUser.FullName,
            Role = _currentUser.Role,
            IsActive = _currentUser.IsActive,
            LastLoginAt = _currentUser.LastLoginAt,
            EmployeeId = _currentUser.EmployeeId
        });
    }
    private async Task<HashSet<string>> GetPermissionsForRoleAsync(string roleName)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_permissionCache.TryGetValue(roleName, out var cached))
                return cached;
            await using var db = await _dbFactory.CreateDbContextAsync();
            var permList = await db.RolePermissions
                .Where(rp => rp.RoleName == roleName)
                .Select(rp => rp.Permission.Name)
                .ToListAsync();
            var perms = new HashSet<string>(permList);
            _permissionCache[roleName] = perms;
            _logger.LogInformation("🔐 [AuthService] Loaded {Count} permissions cho role '{Role}'",
                perms.Count, roleName);
            return perms;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    public static void InvalidatePermissionCache(string? roleName = null)
    {
        if (roleName == null) _permissionCache.Clear();
        else _permissionCache.Remove(roleName);
    }
}
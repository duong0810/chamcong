using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using chamcong.Data;
using chamcong.Models;
using chamcong.DTOs;
using chamcong.Constants;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace chamcong.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AttendanceController> _logger;
    private readonly IWebHostEnvironment _env;

    // cơ chế Dependency Injection DI để nhận ApplicationDbContext và ILogger từ container, không cần khởi tạo thủ công.
    public AttendanceController(ApplicationDbContext context, ILogger<AttendanceController> logger, IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    [HttpPost("checkin")]
    public async Task<ActionResult<AttendanceResponseDto>> CheckIn([FromBody] AttendanceDto dto)
    {
        try
        {
            var employee = await _context.Employees.FindAsync(dto.EmployeeId);
            if (employee == null || !employee.IsActive)
                return Ok(new AttendanceResponseDto { Success = false, Message = "Không tìm thấy nhân viên hoặc nhân viên đã nghỉ việc" });

            // ✅ Validate: khuôn mặt nhận diện phải khớp với tài khoản đang đăng nhập
            // Bỏ qua nếu LoggedInUserId không được gửi (Admin/Manager dùng kiosk mode)
            if (dto.LoggedInUserId.HasValue && dto.LoggedInUserId.Value > 0)
            {
                var loggedInUser = await _context.AppUsers.FindAsync(dto.LoggedInUserId.Value);
                if (loggedInUser == null)
                    return Ok(new AttendanceResponseDto { Success = false, Message = "Không tìm thấy tài khoản đăng nhập." });

                // Kiểm tra EmployeeId của tài khoản đăng nhập có khớp với nhân viên được nhận diện không
                if (loggedInUser.EmployeeId.HasValue && loggedInUser.EmployeeId.Value != dto.EmployeeId)
                {
                    _logger.LogWarning(
                        "⚠️ Mismatch: LoggedInUser={UserId} (EmployeeId={UserEmpId}) nhưng nhận diện EmployeeId={RecognizedEmpId}",
                        dto.LoggedInUserId, loggedInUser.EmployeeId, dto.EmployeeId);

                    return Ok(new AttendanceResponseDto
                    {
                        Success = false,
                        Message = "❌ Khuôn mặt không khớp với tài khoản đang đăng nhập. Vui lòng thử lại."
                    });
                }
            }

            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var existingRecord = await _context.AttendanceRecords
                .Where(a => a.EmployeeId == dto.EmployeeId
                         && a.CheckInTime >= todayStart
                         && a.CheckInTime < todayEnd
                         && a.CheckOutTime == null)
                .FirstOrDefaultAsync();

            if (existingRecord != null)
                return Ok(new AttendanceResponseDto { Success = false, Message = AppConstants.AlreadyCheckedInMessage });

            var clientIp = dto.CheckInIp ?? GetClientIp() ?? "unknown";
            var userAgentHeader = Request.Headers["User-Agent"];
            var deviceInfo = dto.CheckInDeviceInfo ?? (userAgentHeader.Count > 0 ? userAgentHeader.ToString() : null);

            _logger.LogInformation("📊 CheckIn - IP: {Ip}, Device: {Device}",
                clientIp,
                deviceInfo != null ? deviceInfo.Substring(0, Math.Min(100, deviceInfo.Length)) : "Unknown");

            var companySettings = await _context.Set<CompanySettings>().FirstOrDefaultAsync();
            double? companyLat = companySettings?.CompanyLat;
            double? companyLng = companySettings?.CompanyLng;

            var record = new AttendanceRecord
            {
                EmployeeId = dto.EmployeeId,
                CheckInTime = DateTime.Now,
                Location = dto.Location,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Accuracy = dto.Accuracy,
                FaceMatchScore = dto.FaceMatchScore,
                CheckInDeviceInfo = deviceInfo,
                CheckInIp = clientIp,
                CheckInLocalIp = dto.CheckInLocalIp,
                IsManualEntry = false,
            };

            if (dto.CheckInDistanceMeters.HasValue)
                record.CheckInDistanceMeters = dto.CheckInDistanceMeters;
            else if (dto.Latitude.HasValue && dto.Longitude.HasValue && companyLat.HasValue && companyLng.HasValue)
                record.CheckInDistanceMeters = ComputeDistanceMeters(companyLat.Value, companyLng.Value, dto.Latitude.Value, dto.Longitude.Value);

            if (!string.IsNullOrWhiteSpace(dto.CheckInFaceImageBase64))
            {
                try
                {
                    var url = await SaveBase64ImageAsync(dto.CheckInFaceImageBase64);
                    record.CheckInFaceImageUrl = url;
                }
                catch (Exception imgEx)
                {
                    _logger.LogWarning(imgEx, "Không lưu được ảnh CheckIn cho employee {EmployeeId}", dto.EmployeeId);
                }
            }

            _context.AttendanceRecords.Add(record);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Nhân viên {EmployeeCode} chấm công vào lúc {Time} tại ({Lat}, {Lon})",
                employee.EmployeeCode, record.CheckInTime, record.Latitude, record.Longitude);

            return Ok(new AttendanceResponseDto { Success = true, Message = AppConstants.CheckInSuccessMessage, Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chấm công vào cho nhân viên {EmployeeId}", dto.EmployeeId);
            return StatusCode(500, new AttendanceResponseDto { Success = false, Message = "Lỗi server khi xử lý chấm công" });
        }
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<AttendanceResponseDto>> CheckOut([FromBody] AttendanceDto dto)
    {
        try
        {
            // ✅ Validate tương tự CheckIn
            if (dto.LoggedInUserId.HasValue && dto.LoggedInUserId.Value > 0)
            {
                var loggedInUser = await _context.AppUsers.FindAsync(dto.LoggedInUserId.Value);
                if (loggedInUser == null)
                    return Ok(new AttendanceResponseDto { Success = false, Message = "Không tìm thấy tài khoản đăng nhập." });

                if (loggedInUser.EmployeeId.HasValue && loggedInUser.EmployeeId.Value != dto.EmployeeId)
                {
                    _logger.LogWarning(
                        "⚠️ Mismatch CheckOut: LoggedInUser={UserId} (EmployeeId={UserEmpId}) nhưng nhận diện EmployeeId={RecognizedEmpId}",
                        dto.LoggedInUserId, loggedInUser.EmployeeId, dto.EmployeeId);

                    return Ok(new AttendanceResponseDto
                    {
                        Success = false,
                        Message = "❌ Khuôn mặt không khớp với tài khoản đang đăng nhập. Vui lòng thử lại."
                    });
                }
            }

            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var record = await _context.AttendanceRecords
                .Include(a => a.Employee)
                .Where(a => a.EmployeeId == dto.EmployeeId
                         && a.CheckInTime >= todayStart
                         && a.CheckInTime < todayEnd
                         && a.CheckOutTime == null)
                .FirstOrDefaultAsync();

            if (record == null)
                return Ok(new AttendanceResponseDto { Success = false, Message = AppConstants.NotCheckedInMessage });

            record.CheckOutTime = DateTime.Now;

            if (dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                record.CheckOutLatitude = dto.Latitude;
                record.CheckOutLongitude = dto.Longitude;
                record.CheckOutAccuracy = dto.Accuracy;
            }

            var clientIp = dto.CheckOutIp ?? GetClientIp() ?? "unknown";
            var userAgentHeader = Request.Headers["User-Agent"];
            var deviceInfo = dto.CheckOutDeviceInfo ?? (userAgentHeader.Count > 0 ? userAgentHeader.ToString() : null);

            record.CheckOutIp = clientIp;
            record.CheckOutDeviceInfo = deviceInfo;
            record.CheckOutLocalIp = dto.CheckOutLocalIp;

            _logger.LogInformation("📊 CheckOut - IP: {Ip}, Device: {Device}",
                clientIp,
                deviceInfo != null ? deviceInfo.Substring(0, Math.Min(100, deviceInfo.Length)) : "Unknown");

            var companySettings = await _context.Set<CompanySettings>().FirstOrDefaultAsync();
            double? companyLat = companySettings?.CompanyLat;
            double? companyLng = companySettings?.CompanyLng;

            if (dto.CheckOutDistanceMeters.HasValue)
                record.CheckOutDistanceMeters = dto.CheckOutDistanceMeters;
            else if (dto.Latitude.HasValue && dto.Longitude.HasValue && companyLat.HasValue && companyLng.HasValue)
                record.CheckOutDistanceMeters = ComputeDistanceMeters(companyLat.Value, companyLng.Value, dto.Latitude.Value, dto.Longitude.Value);
            else if (record.Latitude.HasValue && record.Longitude.HasValue && dto.Latitude.HasValue && dto.Longitude.HasValue)
                record.CheckOutDistanceMeters = ComputeDistanceMeters(record.Latitude.Value, record.Longitude.Value, dto.Latitude.Value, dto.Longitude.Value);

            if (!string.IsNullOrWhiteSpace(dto.CheckOutFaceImageBase64))
            {
                try
                {
                    var url = await SaveBase64ImageAsync(dto.CheckOutFaceImageBase64);
                    record.CheckOutFaceImageUrl = url;
                }
                catch (Exception imgEx)
                {
                    _logger.LogWarning(imgEx, "Không lưu được ảnh CheckOut cho employee {EmployeeId}", dto.EmployeeId);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Nhân viên {EmployeeCode} chấm công ra lúc {Time}",
                record.Employee!.EmployeeCode, record.CheckOutTime);

            return Ok(new AttendanceResponseDto { Success = true, Message = AppConstants.CheckOutSuccessMessage, Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chấm công ra cho nhân viên {EmployeeId}", dto.EmployeeId);
            return StatusCode(500, new AttendanceResponseDto { Success = false, Message = "Lỗi server khi xử lý chấm công" });
        }
    }

    // GET: api/attendance/history/533
    [HttpGet("history/{employeeId}")]
    // nhận employeeId và tham số truy vấn ngày tùy chọn (mặc định 30 ngày).
    public async Task<ActionResult<IEnumerable<AttendanceHistoryDto>>> GetHistory(int employeeId, [FromQuery] int days = 30)
    {
        try
        {
            var cutoffDate = DateTime.Today.AddDays(-days);

            var query =
                from a in _context.AttendanceRecords.Include(a => a.Employee)
                where a.EmployeeId == employeeId && a.CheckInTime >= cutoffDate
                orderby a.CheckInTime descending
                select new
                {
                    a.Id,
                    EmployeeName = a.Employee!.FullName,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.Location,
                    a.Latitude,
                    a.Longitude,
                    a.CheckOutLatitude,
                    a.CheckOutLongitude,
                    a.FaceMatchScore,
                    a.CheckInFaceImageUrl,
                    a.CheckOutFaceImageUrl,
                    a.CheckInDistanceMeters,
                    a.CheckOutDistanceMeters,
                    a.CheckInDeviceInfo,
                    a.CheckOutDeviceInfo,
                    a.CheckInIp,
                    a.CheckOutIp,
                    a.CheckInLocalIp,
                    a.CheckOutLocalIp,
                    a.Notes,
                    a.IsManualEntry
                };

            var records = await query.ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var dtoList = records.Select(a => new AttendanceHistoryDto
            {
                Id = a.Id,
                EmployeeName = a.EmployeeName,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Location = a.Location,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                CheckOutLatitude = a.CheckOutLatitude,
                CheckOutLongitude = a.CheckOutLongitude,
                FaceMatchScore = a.FaceMatchScore,
                CheckInFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckInFaceImageUrl) ? null : (a.CheckInFaceImageUrl.StartsWith("http") ? a.CheckInFaceImageUrl : $"{baseUrl}{a.CheckInFaceImageUrl}"),
                CheckOutFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckOutFaceImageUrl) ? null : (a.CheckOutFaceImageUrl.StartsWith("http") ? a.CheckOutFaceImageUrl : $"{baseUrl}{a.CheckOutFaceImageUrl}"),
                CheckInDistanceMeters = a.CheckInDistanceMeters,
                CheckOutDistanceMeters = a.CheckOutDistanceMeters,
                CheckInIp = a.CheckInIp,
                CheckOutIp = a.CheckOutIp,
                CheckInDeviceInfo = a.CheckInDeviceInfo,
                CheckOutDeviceInfo = a.CheckOutDeviceInfo,
                CheckInLocalIp = a.CheckInLocalIp,
                CheckOutLocalIp = a.CheckOutLocalIp,
                Notes = a.Notes,
                IsManualEntry = a.IsManualEntry,
            }).ToList();

            return Ok(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy lịch sử chấm công cho nhân viên {EmployeeId}", employeeId);
            return StatusCode(500, "Lỗi server");
        }
    }

    [HttpPut("{id}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] NotesUpdateDto dto)
    {
        try
        {
            var record = await _context.AttendanceRecords.FindAsync(id);
            if (record == null) return NotFound(new { Success = false, Message = "Record not found" });

            record.Notes = dto.Notes;
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Ghi chú đã được cập nhật", Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notes for attendance {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Lỗi server khi cập nhật ghi chú" });
        }
    }

    // GET: api/attendance/my-today/{userId}
    [HttpGet("my-today/{userId}")]
    public async Task<ActionResult<IEnumerable<AttendanceHistoryDto>>> GetMyTodayAttendance(int userId)
    {
        try
        {
            var appUser = await _context.AppUsers.FindAsync(userId);
            if (appUser == null || !appUser.EmployeeId.HasValue)
            {
                _logger.LogWarning("⚠️ my-today: userId={UserId} không có EmployeeId", userId);
                return Ok(new List<AttendanceHistoryDto>());
            }

            var employeeId = appUser.EmployeeId.Value;
            var today = DateTime.Today;

            var records = await (
                from a in _context.AttendanceRecords.Include(a => a.Employee)
                where a.EmployeeId == employeeId && a.CheckInTime >= today
                orderby a.CheckInTime descending
                select a
            ).ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var dtoList = records.Select(a => new AttendanceHistoryDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee!.FullName,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Location = a.Location,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                CheckOutLatitude = a.CheckOutLatitude,
                CheckOutLongitude = a.CheckOutLongitude,
                FaceMatchScore = a.FaceMatchScore,
                CheckInFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckInFaceImageUrl) ? null :
                    (a.CheckInFaceImageUrl.StartsWith("http") ? a.CheckInFaceImageUrl : $"{baseUrl}{a.CheckInFaceImageUrl}"),
                CheckOutFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckOutFaceImageUrl) ? null :
                    (a.CheckOutFaceImageUrl.StartsWith("http") ? a.CheckOutFaceImageUrl : $"{baseUrl}{a.CheckOutFaceImageUrl}"),
                CheckInDistanceMeters = a.CheckInDistanceMeters,
                CheckOutDistanceMeters = a.CheckOutDistanceMeters,
                CheckInDeviceInfo = a.CheckInDeviceInfo,
                CheckOutDeviceInfo = a.CheckOutDeviceInfo,
                Notes = a.Notes
            }).ToList();

            _logger.LogInformation("✅ my-today: userId={UserId} → employeeId={EmpId} → {Count} records",
                userId, employeeId, dtoList.Count);
            return Ok(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ my-today lỗi userId={UserId}", userId);
            return StatusCode(500, "Lỗi server");
        }
    }

    // GET: api/attendance/today
    [HttpGet("today")]  // ← SỬA: bỏ /{userId}, đổi thành "today"
    public async Task<ActionResult<IEnumerable<AttendanceHistoryDto>>> GetTodayAttendance()
    {
        try
        {
            var today = DateTime.Today;

            var query =
                from a in _context.AttendanceRecords.Include(a => a.Employee)
                where a.CheckInTime >= today
                orderby a.CheckInTime descending
                select new
                {
                    a.Id,
                    a.EmployeeId,
                    EmployeeName = a.Employee!.FullName,
                    a.CheckInTime,
                    a.CheckOutTime,
                    a.Location,
                    a.Latitude,
                    a.Longitude,
                    a.CheckOutLatitude,
                    a.CheckOutLongitude,
                    a.FaceMatchScore,
                    a.CheckInFaceImageUrl,
                    a.CheckOutFaceImageUrl,
                    a.CheckInDistanceMeters,
                    a.CheckOutDistanceMeters,
                    a.CheckInDeviceInfo,
                    a.CheckOutDeviceInfo
                };

            var records = await query.ToListAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var dtoList = records.Select(a => new AttendanceHistoryDto
            {
                Id = a.Id,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.EmployeeName,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Location = a.Location,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                CheckOutLatitude = a.CheckOutLatitude,
                CheckOutLongitude = a.CheckOutLongitude,
                FaceMatchScore = a.FaceMatchScore,
                CheckInFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckInFaceImageUrl) ? null :
                    (a.CheckInFaceImageUrl.StartsWith("http") ? a.CheckInFaceImageUrl : $"{baseUrl}{a.CheckInFaceImageUrl}"),
                CheckOutFaceImageUrl = string.IsNullOrWhiteSpace(a.CheckOutFaceImageUrl) ? null :
                    (a.CheckOutFaceImageUrl.StartsWith("http") ? a.CheckOutFaceImageUrl : $"{baseUrl}{a.CheckOutFaceImageUrl}"),
                CheckInDistanceMeters = a.CheckInDistanceMeters,
                CheckOutDistanceMeters = a.CheckOutDistanceMeters,
                CheckInDeviceInfo = a.CheckInDeviceInfo,
                CheckOutDeviceInfo = a.CheckOutDeviceInfo
            }).ToList();

            return Ok(dtoList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách chấm công hôm nay");
            return StatusCode(500, "Lỗi server");
        }
    }

    // PUT: api/attendance/{id}/manual-edit
    [HttpPut("{id}/manual-edit")]
    public async Task<IActionResult> ManualEdit(int id, [FromBody] ManualEditDto dto)
    {
        try
        {
            var record = await _context.AttendanceRecords.FindAsync(id);
            if (record == null)
                return NotFound(new { Success = false, Message = "Không tìm thấy bản ghi chấm công" });

            // Tính lần sửa thứ mấy
            var editCount = await _context.AttendanceEditLogs
                .CountAsync(l => l.AttendanceRecordId == id) + 1;

            // Lưu log trước khi sửa
            var log = new AttendanceEditLog
            {
                AttendanceRecordId = id,
                EditedByUserId = dto.EditedByUserId,
                EditedByUsername = dto.EditedByUsername,
                EditedAt = DateTime.Now,
                OldCheckInTime = record.CheckInTime,
                OldCheckOutTime = record.CheckOutTime,
                NewCheckInTime = dto.NewCheckInTime,
                NewCheckOutTime = dto.NewCheckOutTime,
                Reason = dto.Reason,
                EditCount = editCount
            };
            _context.AttendanceEditLogs.Add(log);

            // Cập nhật bản ghi chấm công
            record.CheckInTime = dto.NewCheckInTime;
            record.CheckOutTime = dto.NewCheckOutTime;
            record.IsManualEntry = true;
            //record.Notes = string.IsNullOrWhiteSpace(dto.Reason)
            //                            ? record.Notes
            //                            : $"[Sửa lần {editCount} bởi {dto.EditedByUsername}]: {dto.Reason}";

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✏️ [ManualEdit] Record {Id} sửa bởi {User} lần {Count}: CheckIn {OldIn}→{NewIn}, CheckOut {OldOut}→{NewOut}",
                id, dto.EditedByUsername, editCount,
                log.OldCheckInTime, dto.NewCheckInTime,
                log.OldCheckOutTime, dto.NewCheckOutTime);

            return Ok(new { Success = true, Message = $"Đã cập nhật chấm công (lần {editCount})", Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [ManualEdit] Lỗi khi sửa record {Id}", id);
            return StatusCode(500, new { Success = false, Message = "Lỗi server khi cập nhật" });
        }
    }

    // GET: api/attendance/{id}/edit-logs
    [HttpGet("{id}/edit-logs")]
    public async Task<ActionResult<IEnumerable<AttendanceEditLogDto>>> GetEditLogs(int id)
    {
        try
        {
            var logs = await _context.AttendanceEditLogs
                .Where(l => l.AttendanceRecordId == id)
                .OrderByDescending(l => l.EditedAt)
                .Select(l => new AttendanceEditLogDto
                {
                    Id = l.Id,
                    EditedByUsername = l.EditedByUsername,
                    EditedAt = l.EditedAt,
                    OldCheckInTime = l.OldCheckInTime,
                    OldCheckOutTime = l.OldCheckOutTime,
                    NewCheckInTime = l.NewCheckInTime,
                    NewCheckOutTime = l.NewCheckOutTime,
                    Reason = l.Reason,
                    EditCount = l.EditCount
                })
                .ToListAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [GetEditLogs] Lỗi khi lấy log record {Id}", id);
            return StatusCode(500, "Lỗi server");
        }
    }

    // Haversine: tính khoảng cách (m) giữa 2 tọa độ
    private static double ComputeDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        static double ToRad(double deg) => deg * Math.PI / 180.0;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    // hỗ trợ chạy sau Proxy (người dùng đặt X-Forwarded-For), ưu tiên header đó, nếu không có thì fallback về RemoteIpAddress.
    private string? GetClientIp()
    {
        // Nếu ứng dụng chạy sau proxy (người dùng đặt X-Forwarded-For), ưu tiên header đó
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var xff) && !string.IsNullOrWhiteSpace(xff))
        {
            // X-Forwarded-For có thể là chuỗi các IP, lấy phần tử đầu (hoặc tuỳ infra lấy phần tử cuối)
            return xff.ToString().Split(',').Select(s => s.Trim()).FirstOrDefault();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private async Task<string> SaveBase64ImageAsync(string dataUrlOrBase64)
    {
        if (string.IsNullOrWhiteSpace(dataUrlOrBase64))
            throw new ArgumentException("Empty image data");

        // Remove data url prefix if present
        var base64 = dataUrlOrBase64.Contains(",") ? dataUrlOrBase64.Split(',')[1] : dataUrlOrBase64;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Invalid base64 image format", ex);
        }

        // giới hạn kthuoc ảnh
        const int maxBytes = 2 * 1024 * 1024;
        if (bytes.Length > maxBytes)
            throw new ArgumentException("Image size exceeds limit");

        var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        if (!Directory.Exists(uploads))
            Directory.CreateDirectory(uploads);

        var fileName = $"{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(uploads, fileName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        // Return relative URL
        var url = $"/uploads/{fileName}";
        return url;
    }
}
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

    public AttendanceController(ApplicationDbContext context, ILogger<AttendanceController> logger, IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    // POST: api/attendance/checkin
    [HttpPost("checkin")]
    public async Task<ActionResult<AttendanceResponseDto>> CheckIn([FromBody] AttendanceDto dto)
    {
        try
        {
            var employee = await _context.Employees.FindAsync(dto.EmployeeId);
            if (employee == null || !employee.IsActive)
            {
                return Ok(new AttendanceResponseDto { Success = false, Message = "Không tìm thấy nhân viên hoặc nhân viên đã nghỉ việc" });
            }

            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var existingQuery =
                from a in _context.AttendanceRecords
                where a.EmployeeId == dto.EmployeeId
                      && a.CheckInTime >= todayStart
                      && a.CheckInTime < todayEnd
                      && a.CheckOutTime == null
                select a;

            var existingRecord = await existingQuery.FirstOrDefaultAsync();

            if (existingRecord != null)
            {
                return Ok(new AttendanceResponseDto { Success = false, Message = AppConstants.AlreadyCheckedInMessage });
            }

            // ✅ Ưu tiên dữ liệu từ DTO (client gửi), fallback sang server
            var clientIp = dto.CheckInIp ?? GetClientIp() ?? "unknown";
            var userAgentHeader = Request.Headers["User-Agent"];
            var deviceInfo = dto.CheckInDeviceInfo ?? (userAgentHeader.Count > 0 ? userAgentHeader.ToString() : null);

            _logger.LogInformation("📊 CheckIn - IP: {Ip}, Device: {Device}",
                clientIp,
                deviceInfo != null ? deviceInfo.Substring(0, Math.Min(100, deviceInfo.Length)) : "Unknown");


            // Load company settings
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
                IsManualEntry = false,
            };

            // Compute CheckInDistanceMeters: priority client -> compute company<->checkin if possible
            if (dto.CheckInDistanceMeters.HasValue)
            {
                record.CheckInDistanceMeters = dto.CheckInDistanceMeters;
            }
            else if (dto.Latitude.HasValue && dto.Longitude.HasValue && companyLat.HasValue && companyLng.HasValue)
            {
                record.CheckInDistanceMeters = ComputeDistanceMeters(companyLat.Value, companyLng.Value, dto.Latitude.Value, dto.Longitude.Value);
            }

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

            _logger.LogInformation("Nhân viên {EmployeeCode} chấm công vào lúc {Time} tại ({Lat}, {Lon})", employee.EmployeeCode, record.CheckInTime, record.Latitude, record.Longitude);

            return Ok(new AttendanceResponseDto { Success = true, Message = AppConstants.CheckInSuccessMessage, Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chấm công vào cho nhân viên {EmployeeId}", dto.EmployeeId);
            return StatusCode(500, new AttendanceResponseDto { Success = false, Message = "Lỗi server khi xử lý chấm công" });
        }
    }

    // POST: api/attendance/checkout
    [HttpPost("checkout")]
    public async Task<ActionResult<AttendanceResponseDto>> CheckOut([FromBody] AttendanceDto dto)
    {
        try
        {
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var recordQuery =
                from a in _context.AttendanceRecords.Include(a => a.Employee)
                where a.EmployeeId == dto.EmployeeId
                      && a.CheckInTime >= todayStart
                      && a.CheckInTime < todayEnd
                      && a.CheckOutTime == null
                select a;

            var record = await recordQuery.FirstOrDefaultAsync();

            if (record == null)
            {
                return Ok(new AttendanceResponseDto { Success = false, Message = AppConstants.NotCheckedInMessage });
            }

            record.CheckOutTime = DateTime.Now;

            if (dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                record.CheckOutLatitude = dto.Latitude;
                record.CheckOutLongitude = dto.Longitude;
                record.CheckOutAccuracy = dto.Accuracy;
            }

            // ✅ Ưu tiên dữ liệu từ DTO (client gửi), fallback sang server
            var clientIp = dto.CheckOutIp ?? GetClientIp() ?? "unknown";
            var userAgentHeader = Request.Headers["User-Agent"];
            var deviceInfo = dto.CheckOutDeviceInfo ?? (userAgentHeader.Count > 0 ? userAgentHeader.ToString() : null);

            record.CheckOutIp = clientIp;
            record.CheckOutDeviceInfo = deviceInfo;

            _logger.LogInformation("📊 CheckOut - IP: {Ip}, Device: {Device}",
                  clientIp,
                  deviceInfo != null ? deviceInfo.Substring(0, Math.Min(100, deviceInfo.Length)) : "Unknown");
            // Load company settings
            var companySettings = await _context.Set<CompanySettings>().FirstOrDefaultAsync();
            double? companyLat = companySettings?.CompanyLat;
            double? companyLng = companySettings?.CompanyLng;

            // Compute CheckOutDistanceMeters with priority:
            // 1) dto provided -> use it
            // 2) company coords available and checkout gps -> compute company <-> checkout
            // 3) fallback: if checkin and checkout coords present -> compute between them
            if (dto.CheckOutDistanceMeters.HasValue)
            {
                record.CheckOutDistanceMeters = dto.CheckOutDistanceMeters;
            }
            else if (dto.Latitude.HasValue && dto.Longitude.HasValue && companyLat.HasValue && companyLng.HasValue)
            {
                record.CheckOutDistanceMeters = ComputeDistanceMeters(companyLat.Value, companyLng.Value, dto.Latitude.Value, dto.Longitude.Value);
            }
            else if (record.Latitude.HasValue && record.Longitude.HasValue && dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                record.CheckOutDistanceMeters = ComputeDistanceMeters(record.Latitude.Value, record.Longitude.Value, dto.Latitude.Value, dto.Longitude.Value);
            }

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

            // ✅ Sửa lỗi CS8602: Thêm null-forgiving operator vì đã Include Employee
            _logger.LogInformation("Nhân viên {EmployeeCode} chấm công ra lúc {Time}", record.Employee!.EmployeeCode, record.CheckOutTime);

            return Ok(new AttendanceResponseDto { Success = true, Message = AppConstants.CheckOutSuccessMessage, Record = record });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chấm công ra cho nhân viên {EmployeeId}", dto.EmployeeId);
            return StatusCode(500, new AttendanceResponseDto { Success = false, Message = "Lỗi server khi xử lý chấm công" });
        }
    }

    // GET: api/attendance/history/5
    [HttpGet("history/{employeeId}")]
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
                    // ✅ Sửa lỗi CS8602: Thêm null-forgiving operator vì đã Include Employee
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
                    a.Notes
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
                Notes = a.Notes
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

    [HttpGet("today")]
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
                    // ✅ Sửa lỗi CS8602: Thêm null-forgiving operator vì đã Include Employee
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

    // Haversine: tính khoảng cách (m) giữa 2 tọa độ
    private static double ComputeDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // meters
        static double ToRad(double deg) => deg * Math.PI / 180.0;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

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

        // Limit size  (adjust as needed)
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
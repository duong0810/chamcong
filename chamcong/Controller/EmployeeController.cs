using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using chamcong.Data;
using chamcong.Models;
using chamcong.DTOs;
using System.Text.Json;

namespace chamcong.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeeController> _logger;

    public EmployeeController(ApplicationDbContext context, ILogger<EmployeeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/employee
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAll(CancellationToken ct = default)
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FullName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                Department = e.Department,
                Position = e.Position,
                HasFace = !string.IsNullOrEmpty(e.FaceDescriptor),
                IsActive = e.IsActive

            })
            .ToListAsync(ct);

        return Ok(employees);
    }

    // GET: api/employee/inactive — lấy nhân viên đã nghỉ việc
    [HttpGet("inactive")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetInactive(CancellationToken ct = default)
    {
        var employees = await _context.Employees
            .Where(e => !e.IsActive)
            .AsNoTracking()
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FullName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                Department = e.Department,
                Position = e.Position,
                HasFace = e.FaceDescriptor != null,
                IsActive = e.IsActive
            })
            .ToListAsync(ct);
        return Ok(employees);
    }

    // GET: api/employee/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeDto>> GetById(int id, CancellationToken ct = default)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FullName,
                Email = e.Email,
                PhoneNumber = e.PhoneNumber,
                Department = e.Department,
                Position = e.Position,
                HasFace = !string.IsNullOrEmpty(e.FaceDescriptor),
                IsActive = e.IsActive
            })
            .FirstOrDefaultAsync(ct);

        if (employee == null)
            return NotFound(new { message = "Không tìm thấy nhân viên" });

        return Ok(employee);
    }

    // PUT: api/employee/{id}/restore — khôi phục nhân viên nghỉ việc
    [HttpPut("{id}/restore")]
    public async Task<ActionResult> Restore(int id, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FindAsync(new object[] { id }, ct);
        if (employee == null)
            return NotFound(new { Success = false, Message = "Không tìm thấy nhân viên" });

        employee.IsActive = true;
        var linkedUser = await _context.AppUsers
            .FirstOrDefaultAsync(u => u.EmployeeId == employee.Id);

        if (linkedUser != null && !linkedUser.IsActive)
        {
            linkedUser.IsActive = true;
            _logger.LogInformation(
                "🔓 Tài khoản '{Username}' được mở khoá vì nhân viên '{Name}' đã được khôi phục.",
                linkedUser.Username, employee.FullName);
        }
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Đã khôi phục nhân viên: {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);
        return Ok(new { Success = true, Message = $"Đã khôi phục nhân viên {employee.FullName}" });
    }

    // POST: api/employee
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmployeeDto dto, CancellationToken ct)
    {
        // ✅ Tự động sinh mã nhân viên nếu client không gửi lên
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            var existingCodes = await _context.Employees
                .Select(e => e.EmployeeCode)
                .ToListAsync(ct);

            int maxNumber = existingCodes
                .Where(c => c != null &&
                            c.StartsWith("NV") &&
                            c.Length == 5 &&
                            int.TryParse(c.Substring(2), out _))
                .Select(c => int.Parse(c.Substring(2)))
                .DefaultIfEmpty(0)
                .Max();

            dto.EmployeeCode = $"NV{(maxNumber + 1):D3}";
        }

        // Kiểm tra mã nhân viên trùng
        var codeExists = await _context.Employees
            .AnyAsync(e => e.EmployeeCode == dto.EmployeeCode, ct);
        if (codeExists)
            return Conflict(new { message = $"Mã nhân viên '{dto.EmployeeCode}' đã tồn tại." });

        // Kiểm tra email đã tồn tại chưa (kể cả nhân viên đã nghỉ việc)
        var emailExists = await _context.Employees
            .AnyAsync(e => e.Email == dto.Email, ct);
        if (emailExists)
            return Conflict(new { message = $"Email '{dto.Email}' đã được sử dụng bởi một nhân viên khác." });

        // Kiểm tra số điện thoại trùng (chỉ khi có nhập)
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var phoneExists = await _context.Employees
                .AnyAsync(e => e.PhoneNumber == dto.PhoneNumber, ct);
            if (phoneExists)
                return Conflict(new { message = $"Số điện thoại '{dto.PhoneNumber}' đã được sử dụng bởi một nhân viên khác." });
        }

        var employee = new Employee
        {
            EmployeeCode = dto.EmployeeCode,
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Department = dto.Department,
            Position = dto.Position,
            IsActive = true
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Đã thêm nhân viên mới: {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);

        return Ok(new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Email = employee.Email,
            PhoneNumber = employee.PhoneNumber,
            Department = employee.Department,
            Position = employee.Position,
            IsActive = employee.IsActive
        });
    }

    // PUT: api/employee/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, [FromBody] EmployeeDto dto, CancellationToken ct = default)
    {
        if (dto == null)
            return BadRequest(new { Success = false, Message = "Payload rỗng" });
        var employee = await _context.Employees.FindAsync(new object[] { id }, ct);
        if (employee == null)
        {
            return NotFound(new { Success = false, Message = "Không tìm thấy nhân viên" });
        }
        employee.EmployeeCode = dto.EmployeeCode ?? employee.EmployeeCode;
        employee.FullName = dto.FullName ?? employee.FullName;
        employee.Email = dto.Email ?? employee.Email;
        employee.PhoneNumber = dto.PhoneNumber ?? employee.PhoneNumber;
        employee.Department = dto.Department ?? employee.Department;
        employee.Position = dto.Position ?? employee.Position;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Đã cập nhật thông tin nhân viên: {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);
        var resultDto = new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Email = employee.Email,
            PhoneNumber = employee.PhoneNumber,
            Department = employee.Department,
            Position = employee.Position
        };
        return Ok(resultDto);
    }

    // DELETE: api/employee/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        var employee = await _context.Employees.FindAsync(new object[] { id }, ct);
        if (employee == null)
        {
            return NotFound(new { Success = false, Message = "Không tìm thấy nhân viên" });
        }
        employee.IsActive = false; // Soft delete
        var linkedUser = await _context.AppUsers
            .FirstOrDefaultAsync(u => u.EmployeeId == employee.Id);

        if (linkedUser != null && linkedUser.IsActive)
        {
            linkedUser.IsActive = false;
            _logger.LogInformation(
                "🔒 Tài khoản '{Username}' bị khoá vì nhân viên '{Name}' đã nghỉ việc.",
                linkedUser.Username, employee.FullName);
        }
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Đã xóa nhân viên: {EmployeeCode} ({FullName})", employee.EmployeeCode, employee.FullName);
        return Ok(new { Success = true, Message = $"Đã xóa nhân viên {employee.FullName}" });
    }

  
    // POST: api/employee/{id}/register-face
    [HttpPost("{id}/register-face")]
    public async Task<ActionResult> RegisterFace(int id, [FromBody] FaceRegistrationDto dto, CancellationToken ct = default)
    {
        try
        {
            if (dto == null)
                return BadRequest(new { Success = false, Message = "Payload rỗng" });

            if (dto.FaceDescriptor == null || dto.FaceDescriptor.Length != 128)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Face descriptor không hợp lệ (có {dto.FaceDescriptor?.Length ?? 0} số, cần 128)"
                });
            }

            var employee = await _context.Employees.FindAsync(new object[] { id }, ct);
            if (employee == null)
            {
                return NotFound(new { Success = false, Message = "Không tìm thấy nhân viên" });
            }

            // Lưu FaceDescriptor dưới dạng JSON string
            employee.FaceDescriptor = JsonSerializer.Serialize(dto.FaceDescriptor);

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Đã đăng ký khuôn mặt cho nhân viên {EmployeeCode} ({FullName})",
                employee.EmployeeCode, employee.FullName);

            return Ok(new
            {
                Success = true,
                Message = $"Đăng ký khuôn mặt thành công cho {employee.FullName}!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi đăng ký khuôn mặt cho nhân viên {EmployeeId}", id);
            return StatusCode(500, new { Success = false, Message = "Lỗi server" });
        }
    }

    // GET: api/employee/{id}/face-descriptor
    [HttpGet("{id}/face-descriptor")]
    public async Task<ActionResult<double[]>> GetFaceDescriptor(int id, CancellationToken ct = default)
    {
        try
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new { e.FaceDescriptor })
                .FirstOrDefaultAsync(ct);

            if (employee == null)
            {
                return NotFound(new { Message = "Không tìm thấy nhân viên" });
            }

            if (string.IsNullOrEmpty(employee.FaceDescriptor))
            {
                return NotFound(new { Message = "Nhân viên chưa đăng ký khuôn mặt" });
            }

            double[]? descriptor;
            try
            {
                descriptor = JsonSerializer.Deserialize<double[]>(employee.FaceDescriptor);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "FaceDescriptor JSON parse error for employee {EmployeeId}", id);
                return StatusCode(500, new { Message = "Dữ liệu face descriptor bị hỏng" });
            }

            if (descriptor == null)
                return StatusCode(500, new { Message = "Dữ liệu face descriptor không hợp lệ" });

            return Ok(descriptor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy face descriptor cho nhân viên {EmployeeId}", id);
            return StatusCode(500, new { Message = "Lỗi server" });
        }
    }
}
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
                Position = e.Position
            })
            .ToListAsync(ct);

        return Ok(employees);
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
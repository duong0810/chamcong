using chamcong.Data;
using chamcong.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace chamcong.Controller;

[ApiController]
[Route("api/department")]
public class DepartmentController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DepartmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/department
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll(CancellationToken ct = default)
    {
        var departments = await _context.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto { Id = d.Id, Name = d.Name })
            .ToListAsync(ct);
        return Ok(departments);
    }

    // POST: api/department
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] DepartmentDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { Message = "Tên phòng ban không được để trống" });

        var exists = await _context.Departments.AnyAsync(d => d.Name == dto.Name, ct);
        if (exists)
            return BadRequest(new { Message = "Phòng ban đã tồn tại" });

        var dept = new Models.Department { Name = dto.Name.Trim() };
        _context.Departments.Add(dept);
        await _context.SaveChangesAsync(ct);

        return Ok(new DepartmentDto { Id = dept.Id, Name = dept.Name });
    }

    // DELETE: api/department/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        var dept = await _context.Departments.FindAsync(new object[] { id }, ct);
        if (dept == null)
            return NotFound(new { Message = "Không tìm thấy phòng ban" });

        dept.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return Ok(new { Success = true });
    }
}
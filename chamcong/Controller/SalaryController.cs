using chamcong.Data;
using chamcong.DTOs;
using chamcong.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace chamcong.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalaryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalaryController> _logger;

    public SalaryController(ApplicationDbContext context, ILogger<SalaryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET: api/salary/attendance-month?month=5&year=2025
    // Trả về bảng chấm công tháng (tất cả nhân viên đang hoạt động)
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("attendance-month")]
    public async Task<IActionResult> GetMonthlyAttendance([FromQuery] int month, [FromQuery] int year)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var employees = await _context.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync();

        var records = await _context.AttendanceRecords
            .Where(a => a.CheckInTime >= from && a.CheckInTime < to)
            .ToListAsync();

        var rows = employees.Select(emp =>
        {
            var empRecords = records.Where(r => r.EmployeeId == emp.Id).ToList();
            var dailyStatus = new Dictionary<int, string>();

            // Điền ký hiệu cho từng ngày
            for (int day = 1; day <= DateTime.DaysInMonth(year, month); day++)
            {
                var date = new DateTime(year, month, day);
                var rec = empRecords.FirstOrDefault(r => r.CheckInTime.Date == date.Date);
                if (rec != null)
                    dailyStatus[day] = rec.CheckOutTime.HasValue ? "✓" : "V"; // V = chưa checkout
                // Ngày không có record → không thêm vào dict (frontend hiển thị trống)
            }

            return new MonthlyAttendanceRowDto
            {
                EmployeeId = emp.Id,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                Position = emp.Position,
                DailyStatus = dailyStatus,
                TotalWorkDays = dailyStatus.Count(d => d.Value == "✓")
            };
        }).ToList();

        return Ok(rows);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET: api/salary?month=5&year=2025
    // Trả về bảng lương tháng (tất cả nhân viên, kèm tính toán tự động)
    // ─────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetSalaryList([FromQuery] int month, [FromQuery] int year)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        int daysInMonth = DateTime.DaysInMonth(year, month);

        var employees = await _context.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync();

        var records = await _context.AttendanceRecords
            .Where(a => a.CheckInTime >= from && a.CheckInTime < to)
            .ToListAsync();

        var salaryDbRecords = await _context.SalaryRecords
            .Where(s => s.Month == month && s.Year == year)
            .ToListAsync();

        var result = employees.Select(emp =>
        {
            var empRecords = records.Where(r => r.EmployeeId == emp.Id).ToList();
            var salaryDb = salaryDbRecords.FirstOrDefault(s => s.EmployeeId == emp.Id);

            // Tính chấm công ngày
            var dailyAttendance = new Dictionary<int, string>();
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                var rec = empRecords.FirstOrDefault(r => r.CheckInTime.Date == date.Date);
                if (rec != null)
                    dailyAttendance[day] = rec.CheckOutTime.HasValue ? "✓" : "V";
            }

            int totalWorkDays = dailyAttendance.Count(d => d.Value == "✓");

            // Lấy thông số từ DB hoặc mặc định
            decimal basicSalary = salaryDb?.BasicSalary ?? 0;
            int requiredWorkDays = salaryDb?.RequiredWorkDays ?? 26;
            decimal dailyWage = requiredWorkDays > 0 ? Math.Round(basicSalary / requiredWorkDays, 2) : 0;
            decimal lunchPrice = salaryDb?.LunchPrice ?? 0;
            decimal overtimeDayPay = salaryDb?.OvertimeDayPay ?? 0;
            int overtimeDays = salaryDb?.OvertimeDays ?? 0;
            bool hasInsurance = salaryDb?.HasInsurance ?? false;
            bool hasAttendanceBonus = totalWorkDays >= requiredWorkDays;

            // Tính các khoản tự động
            decimal salaryByWorkDays = totalWorkDays * dailyWage;
            decimal totalLunchMoney = totalWorkDays * lunchPrice;
            decimal totalOvertimePay = overtimeDays * overtimeDayPay;
            decimal healthInsurance = hasInsurance ? Math.Round(basicSalary * 0.015m, 2) : 0;
            decimal unemploymentInsurance = hasInsurance ? Math.Round(basicSalary * 0.01m, 2) : 0;
            decimal socialInsurance = hasInsurance ? Math.Round(basicSalary * 0.175m, 2) : 0;

            decimal attendanceBonus = salaryDb?.AttendanceBonusAmount ?? 0;
            // Tổng còn lại
            decimal totalRemaining =
                salaryByWorkDays
                + totalOvertimePay
                + (salaryDb?.ResponsibilitySalary ?? 0)
                + attendanceBonus
                + (salaryDb?.PreviousMonthSalary ?? 0)
                + (salaryDb?.RevenueBonus ?? 0)
                + totalLunchMoney
                - (salaryDb?.Advance ?? 0)
                - (salaryDb?.Damage ?? 0)
                - (salaryDb?.UniformFee ?? 0)               
                - (salaryDb?.Violation ?? 0)
                - healthInsurance
                - unemploymentInsurance
                - socialInsurance;

            // Thực lãnh = Tổng còn lại (có thể tùy chỉnh thêm sau)
            decimal actualPay = totalRemaining;

            return new SalaryRecordDto
            {
                Id = salaryDb?.Id ?? 0,
                EmployeeId = emp.Id,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                Department = emp.Department,
                Position = emp.Position,
                Month = month,
                Year = year,

                TrainingType = salaryDb?.TrainingType,
                LaborType = salaryDb?.LaborType,
                BasicSalary = basicSalary,
                RequiredWorkDays = requiredWorkDays,
                DailyWage = dailyWage,

                TotalWorkDays = totalWorkDays,
                AbsentDays = Math.Max(0, requiredWorkDays - totalWorkDays),

                Advance = salaryDb?.Advance ?? 0,
                Damage = salaryDb?.Damage ?? 0,
                UniformFee = salaryDb?.UniformFee ?? 0,
                LunchPrice = lunchPrice,
                TotalLunchMoney = totalLunchMoney,
                Violation = salaryDb?.Violation ?? 0,

                HasInsurance = hasInsurance,
                HealthInsurance = healthInsurance,
                UnemploymentInsurance = unemploymentInsurance,
                SocialInsurance = socialInsurance,

                OvertimeDays = overtimeDays,
                OvertimeDayPay = overtimeDayPay,
                TotalOvertimePay = totalOvertimePay,

                ResponsibilitySalary = salaryDb?.ResponsibilitySalary ?? 0,
                HasAttendanceBonus = hasAttendanceBonus,
                AttendanceBonusAmount = salaryDb?.AttendanceBonusAmount ?? 0,
                PreviousMonthSalary = salaryDb?.PreviousMonthSalary ?? 0,
                RevenueBonus = salaryDb?.RevenueBonus ?? 0,

                SalaryByWorkDays = salaryByWorkDays,
                TotalRemaining = totalRemaining,
                ActualPay = actualPay,

                JobDescription = salaryDb?.JobDescription,
                Signature = salaryDb?.Signature,
                Note = salaryDb?.Note,

                DailyAttendance = dailyAttendance
            };
        }).ToList();

        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────
    // POST: api/salary — Tạo hoặc cập nhật bảng lương 1 nhân viên
    // ─────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SaveSalary([FromBody] SaveSalaryDto dto)
    {
        var existing = await _context.SalaryRecords
            .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId
                                   && s.Month == dto.Month
                                   && s.Year == dto.Year);

        if (existing == null)
        {
            var newRecord = new SalaryRecord
            {
                EmployeeId = dto.EmployeeId,
                Month = dto.Month,
                Year = dto.Year,
                TrainingType = dto.TrainingType,
                LaborType = dto.LaborType,
                BasicSalary = dto.BasicSalary,
                RequiredWorkDays = dto.RequiredWorkDays,
                Advance = dto.Advance,
                Damage = dto.Damage,
                UniformFee = dto.UniformFee,
                LunchPrice = dto.LunchPrice,
                Violation = dto.Violation,
                HasInsurance = dto.HasInsurance,
                OvertimeDays = dto.OvertimeDays,
                OvertimeDayPay = dto.OvertimeDayPay,
                ResponsibilitySalary = dto.ResponsibilitySalary,
                AttendanceBonusAmount = dto.AttendanceBonusAmount,
                PreviousMonthSalary = dto.PreviousMonthSalary,
                RevenueBonus = dto.RevenueBonus,
                JobDescription = dto.JobDescription,
                Signature = dto.Signature,
                Note = dto.Note,
                CreatedAt = DateTime.Now
            };
            _context.SalaryRecords.Add(newRecord);
            _logger.LogInformation("✅ Tạo bảng lương mới: EmployeeId={Id} {Month}/{Year}", dto.EmployeeId, dto.Month, dto.Year);
        }
        else
        {
            existing.TrainingType = dto.TrainingType;
            existing.LaborType = dto.LaborType;
            existing.BasicSalary = dto.BasicSalary;
            existing.RequiredWorkDays = dto.RequiredWorkDays;
            existing.Advance = dto.Advance;
            existing.Damage = dto.Damage;
            existing.UniformFee = dto.UniformFee;
            existing.LunchPrice = dto.LunchPrice;
            existing.Violation = dto.Violation;
            existing.HasInsurance = dto.HasInsurance;
            existing.OvertimeDays = dto.OvertimeDays;
            existing.OvertimeDayPay = dto.OvertimeDayPay;
            existing.ResponsibilitySalary = dto.ResponsibilitySalary;
            existing.AttendanceBonusAmount = dto.AttendanceBonusAmount;
            existing.PreviousMonthSalary = dto.PreviousMonthSalary;
            existing.RevenueBonus = dto.RevenueBonus;
            existing.JobDescription = dto.JobDescription;
            existing.Signature = dto.Signature;
            existing.Note = dto.Note;
            existing.UpdatedAt = DateTime.Now;
            _logger.LogInformation("✏️ Cập nhật bảng lương: EmployeeId={Id} {Month}/{Year}", dto.EmployeeId, dto.Month, dto.Year);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã lưu thành công." });
    }

    // ─────────────────────────────────────────────────────────────────
    // GET: api/salary/export-employee?employeeId=1&month=5&year=2025
    // Export bảng lương 1 nhân viên ra Excel
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("export-employee")]
    public async Task<IActionResult> ExportEmployeeSalary(
    [FromQuery] int employeeId,
    [FromQuery] int month,
    [FromQuery] int year)
    {
        try
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            int daysInMonth = DateTime.DaysInMonth(year, month);

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
                return NotFound(new { message = "Không tìm thấy nhân viên" });

            var records = await _context.AttendanceRecords
                .Where(a => a.EmployeeId == employeeId && a.CheckInTime >= from && a.CheckInTime < to)
                .ToListAsync();

            var salaryDb = await _context.SalaryRecords
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.Month == month && s.Year == year);

            // ── Tính toán (giống GetSalaryList) ──────────────────────
            var dailyAttendance = new Dictionary<int, string>();
            for (int day = 1; day <= daysInMonth; day++)
            {
                var rec = records.FirstOrDefault(r => r.CheckInTime.Date == new DateTime(year, month, day));
                if (rec != null)
                    dailyAttendance[day] = rec.CheckOutTime.HasValue ? "✓" : "V";
            }

            int totalWorkDays = dailyAttendance.Count(d => d.Value == "✓");
            decimal basicSalary = salaryDb?.BasicSalary ?? 0;
            int requiredWorkDays = salaryDb?.RequiredWorkDays ?? 26;
            decimal dailyWage = requiredWorkDays > 0 ? Math.Round(basicSalary / requiredWorkDays, 2) : 0;
            decimal lunchPrice = salaryDb?.LunchPrice ?? 0;
            decimal overtimeDayPay = salaryDb?.OvertimeDayPay ?? 0;
            int overtimeDays = salaryDb?.OvertimeDays ?? 0;
            bool hasInsurance = salaryDb?.HasInsurance ?? false;
            bool hasAttendanceBonus = totalWorkDays >= requiredWorkDays;

            decimal salaryByWorkDays = totalWorkDays * dailyWage;
            decimal totalLunchMoney = totalWorkDays * lunchPrice;
            decimal totalOvertimePay = overtimeDays * overtimeDayPay;
            decimal healthInsurance = hasInsurance ? Math.Round(basicSalary * 0.015m, 2) : 0;
            decimal unemploymentInsurance = hasInsurance ? Math.Round(basicSalary * 0.01m, 2) : 0;
            decimal socialInsurance = hasInsurance ? Math.Round(basicSalary * 0.175m, 2) : 0;

            // <-- CHỈ SỬA Ở ĐÂY: luôn lấy giá trị đã nhập từ DB
            decimal attendanceBonus = salaryDb?.AttendanceBonusAmount ?? 0;

            decimal totalRemaining =
                salaryByWorkDays + totalOvertimePay
                + (salaryDb?.ResponsibilitySalary ?? 0) + attendanceBonus
                + (salaryDb?.PreviousMonthSalary ?? 0) + (salaryDb?.RevenueBonus ?? 0)
                + totalLunchMoney
                - (salaryDb?.Advance ?? 0) - (salaryDb?.Damage ?? 0)
                - (salaryDb?.UniformFee ?? 0) - (salaryDb?.Violation ?? 0)
                - healthInsurance - unemploymentInsurance - socialInsurance;

            // ── Tạo file Excel ────────────────────────────────────────
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add($"Lương {month}-{year}");

            // Style helper
            var headerColor = ClosedXML.Excel.XLColor.FromHtml("#512bd4");
            var subColor = ClosedXML.Excel.XLColor.FromHtml("#e8e3f7");
            var yellowColor = ClosedXML.Excel.XLColor.FromHtml("#fff3cd");
            var greenColor = ClosedXML.Excel.XLColor.FromHtml("#d4edda");
            var whiteFont = ClosedXML.Excel.XLColor.White;

            int row = 1;

            // ── Tiêu đề chính ─────────────────────────────────────────
            var titleCell = ws.Cell(row, 1);
            titleCell.Value = $"BẢNG LƯƠNG THÁNG {month}/{year}";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 14;
            titleCell.Style.Font.FontColor = headerColor;
            ws.Range(row, 1, row, 3).Merge();
            row++;

            // ── Thông tin nhân viên ───────────────────────────────────
            void WriteInfo(int r, string label, string? value)
            {
                ws.Cell(r, 1).Value = label;
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 2).Value = value ?? "-";
                ws.Range(r, 2, r, 3).Merge();
            }

            WriteInfo(row++, "Mã nhân viên:", employee.EmployeeCode);
            WriteInfo(row++, "Họ và tên:", employee.FullName);
            WriteInfo(row++, "Phòng ban:", employee.Department);
            WriteInfo(row++, "Công việc (Chức vụ):", employee.Position);
            WriteInfo(row++, "Loại đào tạo:", salaryDb?.TrainingType);
            WriteInfo(row++, "Loại lao động:", salaryDb?.LaborType);
            row++;

            // ── Bảng lương chi tiết ───────────────────────────────────
            var salTitle = ws.Cell(row, 1);
            salTitle.Value = "CHI TIẾT LƯƠNG";
            salTitle.Style.Font.Bold = true;
            salTitle.Style.Fill.BackgroundColor = headerColor;
            salTitle.Style.Font.FontColor = whiteFont;
            ws.Range(row, 1, row, 3).Merge();
            row++;

            void WriteRow(string label, decimal value, bool bold = false, string? colorHex = null)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 1).Style.Font.Bold = bold;
                var vc = ws.Cell(row, 2);
                vc.Value = (double)value;
                vc.Style.NumberFormat.Format = "#,##0";
                vc.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
                if (bold) vc.Style.Font.Bold = true;
                if (colorHex != null) vc.Style.Font.FontColor = ClosedXML.Excel.XLColor.FromHtml(colorHex);
                row++;
            }

            WriteRow("Lương căn bản (đ):", basicSalary);
            WriteRow("Số ngày phải đi làm:", requiredWorkDays > 0 ? requiredWorkDays : 0);
            WriteRow("Tiền công ngày (đ):", dailyWage);
            WriteRow("Tổng ngày đi làm:", totalWorkDays);
            WriteRow("Ngày nghỉ:", Math.Max(0, requiredWorkDays - totalWorkDays));
            WriteRow("Lương theo ngày đi làm (đ):", salaryByWorkDays, bold: true);
            row++; // spacer

            WriteRow("Tạm ứng (đ):", salaryDb?.Advance ?? 0);
            WriteRow("Đền thiệt hại (đ):", salaryDb?.Damage ?? 0);
            WriteRow("Tiền đồng phục (đ):", salaryDb?.UniformFee ?? 0);
            WriteRow("Giá cơm / ngày (đ):", lunchPrice);
            WriteRow("Tiền cơm (đ):", totalLunchMoney);
            WriteRow("Vi phạm (đ):", salaryDb?.Violation ?? 0);
            row++;

            WriteRow("Bảo hiểm YT 1.5% (đ):", healthInsurance);
            WriteRow("Bảo hiểm TN 1% (đ):", unemploymentInsurance);
            WriteRow("BHXH 17.5% (đ):", socialInsurance);
            row++;

            WriteRow("Làm thêm (ngày):", overtimeDays);
            WriteRow("Tiền làm thêm (đ):", totalOvertimePay);
            WriteRow("Lương trách nhiệm (đ):", salaryDb?.ResponsibilitySalary ?? 0);
            WriteRow("Thưởng chuyên cần (đ):", attendanceBonus);
            WriteRow("Lương tháng trước (đ):", salaryDb?.PreviousMonthSalary ?? 0);
            WriteRow("Thưởng doanh thu (đ):", salaryDb?.RevenueBonus ?? 0);
            row++;

            WriteRow("TỔNG CÒN LẠI (đ):", totalRemaining, bold: true, colorHex: "#856404");
            WriteRow("THỰC LÃNH (đ):", totalRemaining, bold: true, colorHex: "#155724");
            row++;

            ws.Cell(row, 1).Value = "Kí nhận:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = salaryDb?.Signature ?? "";
            ws.Range(row, 2, row, 4).Merge();
            ws.Range(row, 2, row, 3).Style.Border.BottomBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            row++;

            if (!string.IsNullOrWhiteSpace(salaryDb?.Note))
            { ws.Cell(row, 1).Value = "Ghi chú:"; ws.Cell(row, 2).Value = salaryDb.Note; row++; }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"BangLuong_{employee.EmployeeCode}_{employee.FullName}_T{month}_{year}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [ExportEmployeeSalary] Lỗi export employeeId={Id}", employeeId);
            return StatusCode(500, new { message = "Lỗi server khi xuất Excel" });
        }
    }
}
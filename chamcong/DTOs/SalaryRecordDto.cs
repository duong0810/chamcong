namespace chamcong.DTOs;

// ── DTO trả về cho client ────────────────────────────────────────────
public class SalaryRecordDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Department { get; set; }
    public string? Position { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }

    // Thông tin lao động
    public string? TrainingType { get; set; }
    public string? LaborType { get; set; }

    // Lương
    public decimal BasicSalary { get; set; }
    public int RequiredWorkDays { get; set; }
    public decimal DailyWage { get; set; }           // = BasicSalary / RequiredWorkDays

    // Chấm công (tính từ AttendanceRecords)
    public int TotalWorkDays { get; set; }           // Tổng ngày đi làm
    public int AbsentDays { get; set; }              // Ngày nghỉ

    // Khoản trừ
    public decimal Advance { get; set; }
    public decimal Damage { get; set; }
    public decimal UniformFee { get; set; }
    public decimal LunchPrice { get; set; }
    public decimal TotalLunchMoney { get; set; }     // = TotalWorkDays × LunchPrice
    public decimal Violation { get; set; }

    // Bảo hiểm
    public bool HasInsurance { get; set; }
    public decimal HealthInsurance { get; set; }     // 1.5% × BasicSalary
    public decimal UnemploymentInsurance { get; set; }// 1% × BasicSalary
    public decimal SocialInsurance { get; set; }     // 17.5% × BasicSalary

    // Làm thêm
    public int OvertimeDays { get; set; }
    public decimal OvertimeDayPay { get; set; }
    public decimal TotalOvertimePay { get; set; }    // = OvertimeDays × OvertimeDayPay

    // Khoản cộng
    public decimal ResponsibilitySalary { get; set; }
    public bool HasAttendanceBonus { get; set; }     // Đạt chuyên cần
    public decimal AttendanceBonusAmount { get; set; }
    public decimal PreviousMonthSalary { get; set; }
    public decimal RevenueBonus { get; set; }

    // Lương theo ngày đi làm
    public decimal SalaryByWorkDays { get; set; }    // = TotalWorkDays × DailyWage

    // Tổng kết
    public decimal TotalRemaining { get; set; }      // Tổng còn lại
    public decimal ActualPay { get; set; }           // THỰC LÃNH

    // Ghi chú
    public string? JobDescription { get; set; }
    public string? Signature { get; set; }
    public string? Note { get; set; }

    // Dữ liệu chấm công ngày (key = ngày 1-31, value = ký hiệu)
    public Dictionary<int, string> DailyAttendance { get; set; } = new();
}

// ── DTO nhận từ client khi lưu ──────────────────────────────────────
public class SaveSalaryDto
{
    public int EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }

    public string? TrainingType { get; set; }
    public string? LaborType { get; set; }
    public decimal BasicSalary { get; set; }
    public int RequiredWorkDays { get; set; } = 26;
    public decimal Advance { get; set; }
    public decimal Damage { get; set; }
    public decimal UniformFee { get; set; }
    public decimal LunchPrice { get; set; }
    public decimal Violation { get; set; }
    public bool HasInsurance { get; set; }
    public int OvertimeDays { get; set; }
    public decimal OvertimeDayPay { get; set; }
    public decimal ResponsibilitySalary { get; set; }
    public decimal AttendanceBonusAmount { get; set; }
    public decimal PreviousMonthSalary { get; set; }
    public decimal RevenueBonus { get; set; }
    public string? JobDescription { get; set; }
    public string? Signature { get; set; }
    public string? Note { get; set; }
}

// ── DTO bảng chấm công tháng ────────────────────────────────────────
public class MonthlyAttendanceRowDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public Dictionary<int, string> DailyStatus { get; set; } = new(); // ngày → ký hiệu
    public int TotalWorkDays { get; set; }
}
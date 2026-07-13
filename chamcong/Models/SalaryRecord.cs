namespace chamcong.Models;

public class SalaryRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }

    // Thông tin lao động
    public string? TrainingType { get; set; }
    public string? LaborType { get; set; }

    // Lương cơ bản
    public decimal BasicSalary { get; set; }
    public int RequiredWorkDays { get; set; } = 26;

    // Các khoản trừ / phụ
    public decimal Advance { get; set; }
    public decimal Damage { get; set; }
    public decimal UniformFee { get; set; }
    public decimal LunchPrice { get; set; }
    public decimal Violation { get; set; }

    // Bảo hiểm
    public bool HasInsurance { get; set; }

    // Làm thêm
    public int OvertimeDays { get; set; }
    public decimal OvertimeDayPay { get; set; }

    // Các khoản cộng thêm
    public decimal ResponsibilitySalary { get; set; }
    public decimal AttendanceBonusAmount { get; set; }
    public decimal PreviousMonthSalary { get; set; }
    public decimal RevenueBonus { get; set; }

    // Ghi chú
    public string? JobDescription { get; set; }
    public string? Signature { get; set; }
    public string? Note { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
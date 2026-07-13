using Microsoft.EntityFrameworkCore;
using chamcong.Models;

namespace chamcong.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<CompanySettings> CompanySettings { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<AppUser> AppUsers { get; set; }
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<LoginLog> LoginLogs { get; set; }
    public DbSet<SalaryRecord> SalaryRecords { get; set; }
    public DbSet<AttendanceEditLog> AttendanceEditLogs { get; set; } 


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình Relationships
        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(a => a.Employee)
            .WithMany(e => e.AttendanceRecords)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppUser>()
            .HasOne(u => u.Employee)
            .WithMany()
            .HasForeignKey(u => u.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Map đúng tên bảng DB
        modelBuilder.Entity<Permission>().ToTable("Permissions");
        modelBuilder.Entity<RolePermission>().ToTable("RolePermissions");


        modelBuilder.Entity<AttendanceEditLog>(entity =>
        {
            entity.ToTable("AttendanceEditLogs");
            entity.HasKey(e => e.Id);

            entity.HasOne<AttendanceRecord>(e => e.AttendanceRecord)
                  .WithMany()
                  .HasForeignKey(e => e.AttendanceRecordId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<AppUser>(e => e.EditedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.EditedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SalaryRecord>(entity =>
        {
            entity.ToTable("SalaryRecords");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.EmployeeId, e.Month, e.Year })
                  .IsUnique()
                  .HasDatabaseName("UQ_SalaryRecords_EmpMonthYear");

            entity.Property(e => e.BasicSalary).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Advance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Damage).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UniformFee).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LunchPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Violation).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OvertimeDayPay).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ResponsibilitySalary).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AttendanceBonusAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PreviousMonthSalary).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevenueBonus).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Employee)
                  .WithMany()
                  .HasForeignKey(e => e.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
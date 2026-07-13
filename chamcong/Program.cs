using chamcong.Components;
using chamcong.Data;
using chamcong.Models;
using chamcong.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
        options.MaxBufferedUnacknowledgedRenderBatches = 20;
    });

builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.MaximumReceiveMessageSize = 256 * 1024;
});

// ✅ Dùng AddDbContextFactory thay AddDbContext để AuthService dùng được bên trong Scoped service
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Vẫn giữ AddDbContext cho Controller (EF cần cả hai)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var baseUrlFromConfig = builder.Configuration["BaseUrl"];
var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrlFromConfig)
    ? "http://localhost:5184/"
    : baseUrlFromConfig;

builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(effectiveBaseUrl);
});

builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);

var app = builder.Build();

// ✅ Seed tài khoản admin mặc định — chạy mỗi lần khởi động, tự tạo lại nếu bị xóa
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    const string adminUsername = "admin";
    const string adminPassword = "ad";

    var existingAdmin = await db.AppUsers
        .FirstOrDefaultAsync(u => u.Username == adminUsername);

    if (existingAdmin == null)
    {
        db.AppUsers.Add(new AppUser
        {
            Username = adminUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
    }
    else
    {
        // ✅ Đảm bảo luôn Active và đúng Role dù bị sửa
        if (!existingAdmin.IsActive || existingAdmin.Role != "Admin")
        {
            existingAdmin.IsActive = true;
            existingAdmin.Role = "Admin";
            await db.SaveChangesAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseCors("AllowAll");
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
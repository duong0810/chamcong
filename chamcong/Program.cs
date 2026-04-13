using chamcong.Services;
using chamcong.Components;
using chamcong.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ✅ Cấu hình SignalR Circuit để tránh timeout khi chấm công
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        // Thời gian giữ circuit sau khi disconnect (3 phút)
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);

        // Timeout cho các lệnh gọi JavaScript (2 phút - đủ cho nhận diện khuôn mặt)
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);

        // Số lượng render batches tối đa có thể buffer
        options.MaxBufferedUnacknowledgedRenderBatches = 20;
    });

// ✅ Cấu hình SignalR Hub để tăng timeout
builder.Services.AddSignalR(options =>
{
    // Client timeout: 90 giây (tăng từ 30s mặc định)
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);

    // Handshake timeout: 30 giây
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);

    // Keep-alive interval: 15 giây (gửi ping để giữ kết nối)
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Kích thước message tối đa: 256KB (đủ cho ảnh base64)
    options.MaximumReceiveMessageSize = 256 * 1024;
});

// Đăng ký DbContext với SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ← THAY ĐỔI: Đăng ký HttpClient/ApiService cho server bằng AddHttpClient
// Thử lấy từ cấu hình trước, nếu không có thì fallback tới http://localhost:5184/
var baseUrlFromConfig = builder.Configuration["BaseUrl"];
var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrlFromConfig) ? "http://localhost:5184/" : baseUrlFromConfig;

builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(effectiveBaseUrl);
});

// Thêm Controllers cho API với JSON options để xử lý cycles
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ignore cycles khi serialize JSON
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        // Giữ tên property như trong C# (PascalCase)
        options.JsonSerializerOptions.PropertyNamingPolicy = null;

        // Cho phép serialize navigation properties
        options.JsonSerializerOptions.MaxDepth = 64;
    });

// Swagger cho API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS cho phép WinForm app gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ✅ Bật logging cho SignalR để debug
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseCors("AllowAll");

// Map API Controllers
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();



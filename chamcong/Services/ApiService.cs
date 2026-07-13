using System.Net.Http.Json;
using chamcong.DTOs;
using chamcong.Models;
using System.Text.Json;

namespace chamcong.Services;

public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    // =============================================
    // EMPLOYEE APIs
    // =============================================
    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id)
    {
        try
        {
            var response = await _http.GetAsync($"api/employee/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<EmployeeDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetEmployeeByIdAsync lỗi: {ex.Message}");
            return null;
        }
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<EmployeeDto>>("api/employee");
            return result ?? new List<EmployeeDto>();
        }
        catch (Exception)
        {
            return new List<EmployeeDto>();
        }
    }

    public async Task<(EmployeeDto? Data, string? ErrorMessage)> CreateEmployeeAsync(EmployeeDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/employee", dto);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EmployeeDto>();
                return (result, null);
            }

            // Đọc message lỗi cụ thể từ server (409 Conflict, v.v.)
            try
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                var errorMsg = error?.GetValueOrDefault("message") ?? "Thêm nhân viên thất bại.";
                return (null, errorMsg);
            }
            catch
            {
                return (null, "Thêm nhân viên thất bại. Vui lòng thử lại.");
            }
        }
        catch (Exception)
        {
            return (null, "Không thể kết nối đến server.");
        }
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/employee/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EmployeeDto>();
                return result ?? new EmployeeDto();
            }
            return new EmployeeDto();
        }
        catch (Exception)
        {
            return new EmployeeDto();
        }
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        try
        {
            var response = await _http.DeleteAsync($"api/employee/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<EmployeeDto>> GetInactiveEmployeesAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<EmployeeDto>>("api/employee/inactive");
            return result ?? new List<EmployeeDto>();
        }
        catch (Exception)
        {
            return new List<EmployeeDto>();
        }
    }

    public async Task<bool> RestoreEmployeeAsync(int id)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/employee/{id}/restore", new { });
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // =============================================
    // ATTENDANCE APIs
    // =============================================

    public async Task<AttendanceResponseDto> CheckInAsync(AttendanceDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/attendance/checkin", dto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AttendanceResponseDto>();
                return result ?? new AttendanceResponseDto { Success = false, Message = "Lỗi khi đọc response" };
            }

            return new AttendanceResponseDto { Success = false, Message = $"Lỗi HTTP: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new AttendanceResponseDto { Success = false, Message = $"Lỗi kết nối: {ex.Message}" };
        }
    }

    public async Task<AttendanceResponseDto> CheckOutAsync(AttendanceDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/attendance/checkout", dto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AttendanceResponseDto>();
                return result ?? new AttendanceResponseDto { Success = false, Message = "Lỗi khi đọc response" };
            }

            return new AttendanceResponseDto { Success = false, Message = $"Lỗi HTTP: {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new AttendanceResponseDto { Success = false, Message = $"Lỗi kết nối: {ex.Message}" };
        }
    }

    public async Task<List<AttendanceHistoryDto>> GetMyTodayAttendanceAsync(int userId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AttendanceHistoryDto>>(
                $"api/attendance/my-today/{userId}");
            return result ?? new List<AttendanceHistoryDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetMyTodayAttendanceAsync error: {ex.Message}");
            return new List<AttendanceHistoryDto>();
        }
    }

    public async Task<List<AttendanceHistoryDto>> GetHistoryAsync(int employeeId, int days = 30)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AttendanceHistoryDto>>(
                $"api/attendance/history/{employeeId}?days={days}");
            return result ?? new List<AttendanceHistoryDto>();
        }
        catch (Exception)
        {
            return new List<AttendanceHistoryDto>();
        }
    }

    public async Task<List<AttendanceHistoryDto>> GetTodayAttendanceAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AttendanceHistoryDto>>("api/attendance/today");
            return result ?? new List<AttendanceHistoryDto>();
        }
        catch (Exception)
        {
            return new List<AttendanceHistoryDto>();
        }
    }

    public async Task<bool> UpdateAttendanceNoteAsync(int recordId, string? notes)
    {
        try
        {
            var payload = new { notes };
            var resp = await _http.PutAsJsonAsync($"api/attendance/{recordId}/notes", payload);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateAttendanceNoteAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message)> ManualEditAttendanceAsync(int recordId, ManualEditDto dto)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/attendance/{recordId}/manual-edit", dto);
            var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            var message = content?.GetValueOrDefault("message")?.ToString() ?? (response.IsSuccessStatusCode ? "Thành công" : "Thất bại");
            return (response.IsSuccessStatusCode, message);
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<List<AttendanceEditLogDto>> GetAttendanceEditLogsAsync(int recordId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AttendanceEditLogDto>>(
                $"api/attendance/{recordId}/edit-logs");
            return result ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetAttendanceEditLogsAsync: {ex.Message}");
            return new();
        }
    }

    // =============================================
    // FACE REGISTRATION APIs
    // =============================================

    public async Task<(bool Success, string Message)> RegisterFaceAsync(int employeeId, double[] faceDescriptor)
    {
        try
        {
            var dto = new FaceRegistrationDto { FaceDescriptor = faceDescriptor };
            var response = await _http.PostAsJsonAsync($"api/employee/{employeeId}/register-face", dto);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                var message = result?["Message"]?.ToString() ?? "Thành công";
                return (true, message);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, $"Lỗi HTTP {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<double[]?> GetFaceDescriptorAsync(int employeeId)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<double[]>($"api/employee/{employeeId}/face-descriptor");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi lấy face descriptor: {ex.Message}");
            return null;
        }
    }

    public async Task<CompanySettingsDto?> GetCompanySettingsAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/settings/company");
            if (res.IsSuccessStatusCode)
            {
                var dto = await res.Content.ReadFromJsonAsync<CompanySettingsDto>();
                Console.WriteLine($"GetCompanySettingsAsync: success, dto present: {dto != null}");
                return dto;
            }

            var body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"GetCompanySettingsAsync: HTTP {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetCompanySettingsAsync error: {ex}");
            return null;
        }
    }

    public async Task<HttpResponseMessage> UpdateCompanySettingsAsync(CompanySettingsDto dto)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = System.Text.Json.JsonSerializer.Serialize(dto, options);
            Console.WriteLine($"UpdateCompanySettingsAsync - request JSON: {json}");

            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _http.PutAsync("api/settings/company", content);

            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"UpdateCompanySettingsAsync: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateCompanySettingsAsync error: {ex}");
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = ex.Message,
                Content = new StringContent(ex.ToString())
            };
            return resp;
        }
    }


    // =============================================
    // DEPARTMENT APIs
    // =============================================

    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<DepartmentDto>>("api/department");
            return result ?? new List<DepartmentDto>();
        }
        catch (Exception)
        {
            return new List<DepartmentDto>();
        }
    }

    public async Task<DepartmentDto?> CreateDepartmentAsync(string name)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/department", new DepartmentDto { Name = name });
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DepartmentDto>();
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // =============================================
    // ACCOUNT APIs
    // =============================================

    public async Task<(AppUserDto? User, string? ErrorMessage)> LoginAsync(LoginDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/account/login", dto);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<AppUserDto>();
                return (user, null);
            }
            try
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                return (null, error?.GetValueOrDefault("message") ?? "Đăng nhập thất bại.");
            }
            catch
            {
                return (null, "Đăng nhập thất bại. Vui lòng thử lại.");
            }
        }
        catch (Exception)
        {
            return (null, "Không thể kết nối đến server.");
        }
    }

    public async Task LogoutAsync(int userId)
    {
        try { await _http.PostAsync($"api/account/{userId}/logout", null); }
        catch { }
    }

    public async Task<List<LoginLogDto>> GetLoginHistoryAsync(int days = 30)
    {
        try
        {
            var response = await _http.GetAsync($"api/account/login-history?days={days}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<LoginLogDto>>() ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ GetLoginHistoryAsync: {ex.Message}");
        }
        return new();
    }

    public async Task<List<AppUserDto>> GetUsersAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<AppUserDto>>("api/account");
            return result ?? new List<AppUserDto>();
        }
        catch (Exception)
        {
            return new List<AppUserDto>();
        }
    }

    public async Task<(AppUserDto? Data, string? ErrorMessage)> CreateUserAsync(CreateUserDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/account", dto);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<AppUserDto>(), null);
            try
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                return (null, error?.GetValueOrDefault("message") ?? "Tạo tài khoản thất bại.");
            }
            catch
            {
                return (null, "Tạo tài khoản thất bại. Vui lòng thử lại.");
            }
        }
        catch (Exception)
        {
            return (null, "Không thể kết nối đến server.");
        }
    }

    public async Task<(bool Success, string Message)> ChangeRoleAsync(int userId, string role)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/account/{userId}/role", new ChangeRoleDto { Role = role });
            var msg = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, msg);
        }
        catch (Exception)
        {
            return (false, "Không thể kết nối đến server.");
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(int userId, string newPassword)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/account/{userId}/password", new ResetPasswordDto { NewPassword = newPassword });
            var msg = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, msg);
        }
        catch (Exception)
        {
            return (false, "Không thể kết nối đến server.");
        }
    }

    public async Task<(bool Success, string Message)> ToggleUserActiveAsync(int userId)
    {
        try
        {
            var response = await _http.PutAsJsonAsync($"api/account/{userId}/toggle", new { });
            var msg = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, msg);
        }
        catch (Exception)
        {
            return (false, "Không thể kết nối đến server.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(int userId)
    {
        try
        {
            var response = await _http.DeleteAsync($"api/account/{userId}");
            var msg = await ReadMessageAsync(response);
            return (response.IsSuccessStatusCode, msg);
        }
        catch (Exception)
        {
            return (false, "Không thể kết nối đến server.");
        }
    }


    // ── Bảng chấm công tháng ─────────────────────────────────────────
    public async Task<List<MonthlyAttendanceRowDto>> GetMonthlyAttendanceAsync(int month, int year)
    {
        try
        {
            var response = await _http.GetAsync($"api/salary/attendance-month?month={month}&year={year}");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<MonthlyAttendanceRowDto>>() ?? new();
        }
        catch { return new(); }
    }

    // ── Bảng lương tháng ─────────────────────────────────────────────
    public async Task<List<SalaryRecordDto>> GetSalaryListAsync(int month, int year)
    {
        try
        {
            var response = await _http.GetAsync($"api/salary?month={month}&year={year}");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<SalaryRecordDto>>() ?? new();
        }
        catch { return new(); }
    }

    // ── Lưu bảng lương 1 nhân viên ───────────────────────────────────
    public async Task<(bool success, string message)> SaveSalaryAsync(SaveSalaryDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/salary", dto);
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            var msg = content.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return (response.IsSuccessStatusCode, msg);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── In bảng lương nhân viên ───────────────────────────────────
    public async Task<(bool Success, byte[]? Data, string FileName, string Error)> ExportEmployeeSalaryAsync(
    int employeeId, int month, int year)
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/salary/export-employee?employeeId={employeeId}&month={month}&year={year}");

            if (!response.IsSuccessStatusCode)
                return (false, null, "", $"Lỗi HTTP {(int)response.StatusCode}");

            var data = await response.Content.ReadAsByteArrayAsync();
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                        ?? response.Content.Headers.ContentDisposition?.FileName
                        ?? $"BangLuong_T{month}_{year}.xlsx";

            return (true, data, fileName.Trim('"'), "");
        }
        catch (Exception ex)
        {
            return (false, null, "", $"Lỗi kết nối: {ex.Message}");
        }
    }

    // Helper đọc message từ response
    private async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return data?.GetValueOrDefault("message") ?? (response.IsSuccessStatusCode ? "Thành công." : "Thao tác thất bại.");
        }
        catch
        {
            return response.IsSuccessStatusCode ? "Thành công." : "Thao tác thất bại.";
        }
    }
}



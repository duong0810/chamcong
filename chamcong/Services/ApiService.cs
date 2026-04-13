using System.Net.Http.Json;
using chamcong.DTOs;
using chamcong.Models;

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

}
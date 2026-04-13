using Microsoft.AspNetCore.Mvc;
using chamcong.Data;
using chamcong.Models;
using chamcong.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace chamcong.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ApplicationDbContext context, ILogger<SettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("company")]
    public async Task<ActionResult<CompanySettingsDto>> GetCompanySettings(CancellationToken ct = default)
    {
        var settings = await _context.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (settings == null)
            return Ok(new CompanySettingsDto());

        return Ok(new CompanySettingsDto
        {
            Id = settings.Id,
            CompanyLocationUrl = settings.CompanyLocationUrl,
            CompanyLat = settings.CompanyLat,
            CompanyLng = settings.CompanyLng,
            LocationAcceptThresholdMeters = settings.LocationAcceptThresholdMeters
        });
    }

    [HttpPut("company")]
    public async Task<ActionResult<CompanySettingsDto>> UpdateCompanySettings([FromBody] CompanySettingsDto dto, CancellationToken ct = default)
    {
        if (dto == null)
            return BadRequest(new { Success = false, Message = "Payload rỗng" });

        _logger.LogInformation("DEBUG UpdateCompanySettings called. Incoming DTO: {@dto}", dto);

        try
        {
            // Nếu client gửi URL nhưng không gửi lat/lng thì cố gắng trích tọa độ tự động
            if (!string.IsNullOrWhiteSpace(dto.CompanyLocationUrl) && (!dto.CompanyLat.HasValue || !dto.CompanyLng.HasValue))
            {
                var (lat, lng) = await ResolveAndParseCoordinatesAsync(dto.CompanyLocationUrl, ct);
                if (lat.HasValue && lng.HasValue)
                {
                    dto.CompanyLat = lat;
                    dto.CompanyLng = lng;
                    _logger.LogInformation("Parsed coordinates from URL: {Lat}, {Lng}", lat, lng);
                }
                else
                {
                    _logger.LogInformation("Could not parse coordinates from URL: {Url}", dto.CompanyLocationUrl);
                }
            }

            var settings = await _context.CompanySettings.FirstOrDefaultAsync(ct);
            if (settings == null)
            {
                settings = new CompanySettings
                {
                    CompanyLocationUrl = dto.CompanyLocationUrl,
                    CompanyLat = dto.CompanyLat,
                    CompanyLng = dto.CompanyLng,
                    LocationAcceptThresholdMeters = dto.LocationAcceptThresholdMeters,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CompanySettings.Add(settings);
            }
            else
            {
                settings.CompanyLocationUrl = dto.CompanyLocationUrl;
                settings.CompanyLat = dto.CompanyLat;
                settings.CompanyLng = dto.CompanyLng;
                settings.LocationAcceptThresholdMeters = dto.LocationAcceptThresholdMeters;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);

            dto.Id = settings.Id;
            return Ok(dto);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "DB update error when saving company settings");
            return StatusCode(500, new { Success = false, Message = "Lỗi khi lưu cấu hình" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when updating company settings");
            return StatusCode(500, new { Success = false, Message = "Lỗi server" });
        }
    }

    // ----- Helpers -----

    private static (double? lat, double? lng) TryParseCoordinatesFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, null);

        string decoded = url;
        try { decoded = Uri.UnescapeDataString(url); } catch { /* ignore */ }

        // 1) Pattern: !3d{lat}!4d{lng}
        var m = Regex.Match(decoded, @"!3d(?<lat>-?\d+(\.\d+)?)!4d(?<lng>-?\d+(\.\d+)?)");
        if (m.Success &&
            double.TryParse(m.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat1) &&
            double.TryParse(m.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng1))
        {
            return (lat1, lng1);
        }

        // 2) Pattern: !2d{lng}!3d{lat}
        m = Regex.Match(decoded, @"!2d(?<lng>-?\d+(\.\d+)?)!3d(?<lat>-?\d+(\.\d+)?)");
        if (m.Success &&
            double.TryParse(m.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat2) &&
            double.TryParse(m.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng2))
        {
            return (lat2, lng2);
        }

        // 3) Pattern: /@lat,lng,...
        m = Regex.Match(decoded, @"@(?<lat>-?\d+(\.\d+)?),\s*(?<lng>-?\d+(\.\d+)?)");
        if (m.Success &&
            double.TryParse(m.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat3) &&
            double.TryParse(m.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng3))
        {
            return (lat3, lng3);
        }

        // 4) Query parameters: q=lat,lng or ll=lat,lng or center=lat,lng or query=lat,lng
        if (Uri.TryCreate(decoded, UriKind.Absolute, out var uri))
        {
            try
            {
                var q = QueryHelpers.ParseQuery(uri.Query);
                if (q.TryGetValue("ll", out var v) || q.TryGetValue("q", out v) || q.TryGetValue("query", out v) || q.TryGetValue("center", out v))
                {
                    var s = v.ToString();
                    if (!string.IsNullOrWhiteSpace(s) && s.Contains(','))
                    {
                        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 &&
                            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat4) &&
                            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng4))
                        {
                            return (lat4, lng4);
                        }
                    }
                }
            }
            catch { /* ignore parse errors */ }
        }

        // 5) Fallback: tìm các số thập phân và lấy cặp cuối cùng
        var coords = Regex.Matches(decoded, @"(-?\d+\.\d+)");
        if (coords.Count >= 2)
        {
            if (double.TryParse(coords[coords.Count - 2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat5) &&
                double.TryParse(coords[coords.Count - 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng5))
            {
                return (lat5, lng5);
            }
        }

        return (null, null);
    }

    // Note: this method is instance (non-static) so it can use _logger
    private async Task<(double? lat, double? lng)> ResolveAndParseCoordinatesAsync(string url, CancellationToken ct)
    {
        // 1) Thử parse trực tiếp trước
        var direct = TryParseCoordinatesFromUrl(url);
        if (direct.lat.HasValue && direct.lng.HasValue) return direct;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return (null, null);

        // Hosts thường là short-links
        var shortHosts = new[] { "goo.gl", "g.page", "maps.app.goo.gl", "bit.ly", "tinyurl.com", "maps.app.goo.gl" };
        bool isShort = shortHosts.Any(h => string.Equals(uri.Host, h, StringComparison.OrdinalIgnoreCase));

        // Nếu là short link hoặc google domain, thử resolve redirect
        bool tryResolve = isShort || uri.Host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("g.co", StringComparison.OrdinalIgnoreCase);

        if (!tryResolve) return (null, null);

        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            var final = resp.RequestMessage?.RequestUri?.ToString() ?? url;
            var parsed = TryParseCoordinatesFromUrl(final);
            return parsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogInformation("ResolveAndParseCoordinatesAsync failed for {Url}: {Msg}", url, ex.Message);
            return (null, null);
        }
    }
}
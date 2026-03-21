using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Data;
using SmartNest.Server.Services;
using SmartNest.Shared.DTOs;
using SmartNest.Shared.Models;

namespace SmartNest.Server.Controllers;

// ─── Auth ─────────────────────────────────────────────────────────────────────
[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest r)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _auth.RegisterAsync(r);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest r)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _auth.LoginAsync(r);
        return result.Success ? Ok(result) : Unauthorized(result);
    }
}

// ─── Sensor ───────────────────────────────────────────────────────────────────
[ApiController, Route("api/sensor"), Authorize]
public class SensorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AlertService _alerts;
    private int Uid => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public SensorController(AppDbContext db, AlertService alerts) { _db = db; _alerts = alerts; }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SensorDataDto dto)
    {
        var e = new SensorData { UserId = Uid, Temperature = dto.Temperature, Humidity = dto.Humidity, Dust = dto.Dust, RecordedAt = DateTime.UtcNow };
        _db.SensorData.Add(e);
        await _db.SaveChangesAsync();
        await _alerts.CheckAndCreateAlertsAsync(Uid, dto);
        return Ok(new SensorDataDto { Temperature = e.Temperature, Humidity = e.Humidity, Dust = e.Dust, RecordedAt = e.RecordedAt });
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest()
    {
        var d = await _db.SensorData.Where(s => s.UserId == Uid).OrderByDescending(s => s.RecordedAt).FirstOrDefaultAsync();
        if (d == null) return NotFound();
        return Ok(new SensorDataDto { Temperature = d.Temperature, Humidity = d.Humidity, Dust = d.Dust, RecordedAt = d.RecordedAt });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int hours = 24)
    {
        var from = DateTime.UtcNow.AddHours(-hours);
        var data = await _db.SensorData
            .Where(s => s.UserId == Uid && s.RecordedAt >= from)
            .OrderByDescending(s => s.RecordedAt).Take(200)
            .Select(s => new SensorDataDto { Temperature = s.Temperature, Humidity = s.Humidity, Dust = s.Dust, RecordedAt = s.RecordedAt })
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats([FromQuery] int hours = 24)
    {
        var from = DateTime.UtcNow.AddHours(-hours);
        var data = await _db.SensorData.Where(s => s.UserId == Uid && s.RecordedAt >= from).ToListAsync();
        if (!data.Any()) return NotFound();
        return Ok(new SensorStatsDto
        {
            AvgTemperature = Math.Round(data.Average(d => d.Temperature), 1),
            MinTemperature = data.Min(d => d.Temperature),
            MaxTemperature = data.Max(d => d.Temperature),
            AvgHumidity    = Math.Round(data.Average(d => d.Humidity), 1),
            MinHumidity    = data.Min(d => d.Humidity),
            MaxHumidity    = data.Max(d => d.Humidity),
            AvgDust        = Math.Round(data.Average(d => d.Dust), 1),
            DataPoints     = data.Count
        });
    }
}

// ─── Chick ────────────────────────────────────────────────────────────────────
[ApiController, Route("api/chick"), Authorize]
public class ChickController : ControllerBase
{
    private readonly AppDbContext _db;
    private int Uid => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public ChickController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChickDto dto)
    {
        var e = new Chick { UserId = Uid, Total = dto.Total, Healthy = dto.Healthy, Sick = dto.Sick, Danger = dto.Danger, RecordedAt = DateTime.UtcNow };
        _db.Chicks.Add(e);
        await _db.SaveChangesAsync();
        return Ok(new ChickDto { Total = e.Total, Healthy = e.Healthy, Sick = e.Sick, Danger = e.Danger, RecordedAt = e.RecordedAt });
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest()
    {
        var c = await _db.Chicks.Where(c => c.UserId == Uid).OrderByDescending(c => c.RecordedAt).FirstOrDefaultAsync();
        if (c == null) return NotFound();
        return Ok(new ChickDto { Total = c.Total, Healthy = c.Healthy, Sick = c.Sick, Danger = c.Danger, RecordedAt = c.RecordedAt });
    }
}

// ─── Notification ─────────────────────────────────────────────────────────────
[ApiController, Route("api/notification"), Authorize]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _db;
    private int Uid => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public NotificationController(AppDbContext db) => _db = db;

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var s = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == Uid);
        if (s == null) return NotFound();
        return Ok(new NotificationSettingDto { EmailEnabled = s.EmailEnabled, PopupEnabled = s.PopupEnabled, MaxTemperature = s.MaxTemperature, MinTemperature = s.MinTemperature, MaxHumidity = s.MaxHumidity, MaxDust = s.MaxDust });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> PutSettings([FromBody] NotificationSettingDto dto)
    {
        var s = await _db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == Uid);
        if (s == null) { s = new NotificationSetting { UserId = Uid }; _db.NotificationSettings.Add(s); }
        s.EmailEnabled = dto.EmailEnabled; s.PopupEnabled = dto.PopupEnabled;
        s.MaxTemperature = dto.MaxTemperature; s.MinTemperature = dto.MinTemperature;
        s.MaxHumidity = dto.MaxHumidity; s.MaxDust = dto.MaxDust;
        await _db.SaveChangesAsync();
        return Ok(dto);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] bool unreadOnly = false)
    {
        var q = _db.Alerts.Where(a => a.UserId == Uid);
        if (unreadOnly) q = q.Where(a => !a.IsRead);
        var list = await q.OrderByDescending(a => a.CreatedAt).Take(50)
            .Select(a => new AlertDto { Id = a.Id, Type = a.Type, Severity = a.Severity, Message = a.Message, IsRead = a.IsRead, CreatedAt = a.CreatedAt })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPut("alerts/{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var a = await _db.Alerts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (a == null) return NotFound();
        a.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("alerts/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var alerts = await _db.Alerts.Where(a => a.UserId == Uid && !a.IsRead).ToListAsync();
        alerts.ForEach(a => a.IsRead = true);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

// ─── Health ───────────────────────────────────────────────────────────────────
[ApiController, Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    public HealthController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Get()
    {
        try { var ok = _db.Database.CanConnect(); return Ok(new { status = "healthy", db = ok ? "connected" : "error", ts = DateTime.UtcNow }); }
        catch (Exception ex) { return StatusCode(503, new { status = "unhealthy", error = ex.Message }); }
    }
}

// ─── WebRTC ICE config ────────────────────────────────────────────────────────
[ApiController, Route("api/webrtc"), Authorize]
public class WebRtcController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public WebRtcController(IConfiguration cfg) => _cfg = cfg;

    [HttpGet("ice-servers")]
    public IActionResult GetIceServers()
    {
        var h = _cfg["WebRTC:TurnHost"]       ?? "openrelay.metered.ca";
        var u = _cfg["WebRTC:TurnUsername"]   ?? "openrelayproject";
        var c = _cfg["WebRTC:TurnCredential"] ?? "openrelayproject";
        return Ok(new object[]
        {
            new { urls = "stun:stun.l.google.com:19302" },
            new { urls = "stun:stun1.l.google.com:19302" },
            new { urls = $"stun:{h}:80" },
            new { urls = $"turn:{h}:80",                   username = u, credential = c },
            new { urls = $"turn:{h}:443",                  username = u, credential = c },
            new { urls = $"turn:{h}:443?transport=tcp",    username = u, credential = c },
            new { urls = $"turns:{h}:443",                 username = u, credential = c },
        });
    }
}

using System.ComponentModel.DataAnnotations;

namespace SmartNest.Shared.DTOs;

// ─── Auth ─────────────────────────────────────────────────────────────────────
public class LoginRequest
{
    [Required] public string Username   { get; set; } = string.Empty;
    [Required] public string Password   { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress]                        public string Email    { get; set; } = string.Empty;
    [Required, MinLength(6)]                        public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool     Success   { get; set; }
    public string   Message   { get; set; } = string.Empty;
    public string   Token     { get; set; } = string.Empty;
    public string   Username  { get; set; } = string.Empty;
    public string   Email     { get; set; } = string.Empty;
    public string   Role      { get; set; } = string.Empty;
    public bool     RememberMe{ get; set; }
    public DateTime ExpiresAt { get; set; }
}

// ─── Sensor ───────────────────────────────────────────────────────────────────
public class SensorDataDto
{
    public double   Temperature { get; set; }
    public double   Humidity    { get; set; }
    public double   Dust        { get; set; }
    public DateTime RecordedAt  { get; set; } = DateTime.UtcNow;
}

public class SensorStatsDto
{
    public double AvgTemperature { get; set; }
    public double MinTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public double AvgHumidity   { get; set; }
    public double MinHumidity   { get; set; }
    public double MaxHumidity   { get; set; }
    public double AvgDust       { get; set; }
    public int    DataPoints    { get; set; }
}

// ─── Chick ────────────────────────────────────────────────────────────────────
public class ChickDto
{
    public int      Total     { get; set; }
    public int      Healthy   { get; set; }
    public int      Sick      { get; set; }
    public int      Danger    { get; set; }
    public DateTime RecordedAt{ get; set; } = DateTime.UtcNow;
}

// ─── Notifications ────────────────────────────────────────────────────────────
public class NotificationSettingDto
{
    public bool   EmailEnabled   { get; set; } = true;
    public bool   PopupEnabled   { get; set; } = true;
    public double MaxTemperature { get; set; } = 35;
    public double MinTemperature { get; set; } = 15;
    public double MaxHumidity    { get; set; } = 80;
    public double MaxDust        { get; set; } = 150;
}

public class AlertDto
{
    public int      Id        { get; set; }
    public string   Type      { get; set; } = string.Empty;
    public string   Severity  { get; set; } = string.Empty;
    public string   Message   { get; set; } = string.Empty;
    public bool     IsRead    { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Video ────────────────────────────────────────────────────────────────────
public class VideoRecordingDto
{
    public string   FileName   { get; set; } = string.Empty;
    public string   Url        { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public long     SizeBytes  { get; set; }
    public string FormattedSize => SizeBytes switch
    {
        < 1_024            => $"{SizeBytes} o",
        < 1_048_576        => $"{SizeBytes / 1024.0:F1} Ko",
        _                  => $"{SizeBytes / 1_048_576.0:F1} Mo"
    };
}

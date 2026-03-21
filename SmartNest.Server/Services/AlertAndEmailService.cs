using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SmartNest.Server.Data;
using SmartNest.Shared.DTOs;
using SmartNest.Shared.Models;

namespace SmartNest.Server.Services;

// ─── Alert Service ────────────────────────────────────────────────────────────

public class AlertService
{
    private readonly AppDbContext       _db;
    private readonly ILogger<AlertService> _log;

    public AlertService(AppDbContext db, ILogger<AlertService> log) { _db = db; _log = log; }

    public async Task CheckAndCreateAlertsAsync(int userId, SensorDataDto data)
    {
        var settings = await _db.NotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null) return;

        var alerts = new List<Alert>();

        if (data.Temperature > settings.MaxTemperature)
            alerts.Add(Build(userId, "temperature", "critical",
                $"Température critique : {data.Temperature:F1}°C (seuil : {settings.MaxTemperature}°C)"));
        else if (data.Temperature < settings.MinTemperature)
            alerts.Add(Build(userId, "temperature", "warning",
                $"Température basse : {data.Temperature:F1}°C (seuil : {settings.MinTemperature}°C)"));

        if (data.Humidity > settings.MaxHumidity)
            alerts.Add(Build(userId, "humidity", "warning",
                $"Humidité élevée : {data.Humidity:F1}% (seuil : {settings.MaxHumidity}%)"));

        if (data.Dust > settings.MaxDust)
            alerts.Add(Build(userId, "dust", "warning",
                $"Particules : {data.Dust:F0} µg/m³ (seuil : {settings.MaxDust} µg/m³)"));

        if (alerts.Count > 0)
        {
            _db.Alerts.AddRange(alerts);
            await _db.SaveChangesAsync();
            _log.LogInformation("{Count} alerte(s) créée(s) pour l'utilisateur {UserId}", alerts.Count, userId);
        }
    }

    private static Alert Build(int userId, string type, string severity, string message) => new()
    {
        UserId    = userId,
        Type      = type,
        Severity  = severity,
        Message   = message,
        CreatedAt = DateTime.UtcNow
    };
}

// ─── Email Service ────────────────────────────────────────────────────────────

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration        _cfg;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration cfg, ILogger<EmailService> log) { _cfg = cfg; _log = log; }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _log.LogInformation("[EMAIL] To:{To} | Subject:{Subject}", to, subject);
            return;
        }
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_cfg["Smtp:From"] ?? "noreply@smartnest.io"));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body    = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, int.Parse(_cfg["Smtp:Port"] ?? "587"), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_cfg["Smtp:Username"], _cfg["Smtp:Password"]);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex) { _log.LogError(ex, "Échec envoi email à {To}", to); }
    }
}

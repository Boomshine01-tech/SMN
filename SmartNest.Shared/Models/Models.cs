using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartNest.Shared.Models;

public class User
{
    public int Id { get; set; }
    [MaxLength(100)] public string Username { get; set; } = string.Empty;
    [MaxLength(200)] public string Email    { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role         { get; set; } = "User";
    public bool   IsActive     { get; set; } = true;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public ICollection<SensorData>        SensorData           { get; set; } = new List<SensorData>();
    public ICollection<Chick>             Chicks               { get; set; } = new List<Chick>();
    public ICollection<Alert>             Alerts               { get; set; } = new List<Alert>();
    public NotificationSetting?           NotificationSetting  { get; set; }
}

public class SensorData
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Range(-50, 100)]  public double Temperature { get; set; }
    [Range(0, 100)]    public double Humidity    { get; set; }
    [Range(0, 1000)]   public double Dust        { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey("UserId")] public User? User { get; set; }
}

public class Chick
{
    public int Id { get; set; }
    public int UserId  { get; set; }
    public int Total   { get; set; }
    public int Healthy { get; set; }
    public int Sick    { get; set; }
    public int Danger  { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey("UserId")] public User? User { get; set; }
}

public class Alert
{
    public int    Id       { get; set; }
    public int    UserId   { get; set; }
    public string Type     { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message  { get; set; } = string.Empty;
    public bool   IsRead   { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey("UserId")] public User? User { get; set; }
}

public class NotificationSetting
{
    public int    Id             { get; set; }
    public int    UserId         { get; set; }
    public bool   EmailEnabled   { get; set; } = true;
    public bool   PopupEnabled   { get; set; } = true;
    public double MaxTemperature { get; set; } = 35;
    public double MinTemperature { get; set; } = 15;
    public double MaxHumidity    { get; set; } = 80;
    public double MaxDust        { get; set; } = 150;
    [ForeignKey("UserId")] public User? User { get; set; }
}

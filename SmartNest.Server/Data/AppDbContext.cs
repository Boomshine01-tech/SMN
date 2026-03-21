using Microsoft.EntityFrameworkCore;
using SmartNest.Shared.Models;

namespace SmartNest.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>               Users               => Set<User>();
    public DbSet<SensorData>         SensorData          => Set<SensorData>();
    public DbSet<Chick>              Chicks              => Set<Chick>();
    public DbSet<Alert>              Alerts              => Set<Alert>();
    public DbSet<NotificationSetting>NotificationSettings=> Set<NotificationSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<NotificationSetting>()
            .HasOne(n => n.User).WithOne(u => u.NotificationSetting)
            .HasForeignKey<NotificationSetting>(n => n.UserId);

        b.Entity<SensorData>()
            .HasOne(s => s.User).WithMany(u => u.SensorData)
            .HasForeignKey(s => s.UserId);

        b.Entity<Chick>()
            .HasOne(c => c.User).WithMany(u => u.Chicks)
            .HasForeignKey(c => c.UserId);

        b.Entity<Alert>()
            .HasOne(a => a.User).WithMany(u => u.Alerts)
            .HasForeignKey(a => a.UserId);
    }
}

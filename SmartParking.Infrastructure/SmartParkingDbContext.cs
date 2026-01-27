using Microsoft.EntityFrameworkCore;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure;

public sealed class SmartParkingDbContext : DbContext
{
    public SmartParkingDbContext(DbContextOptions<SmartParkingDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<Slot> Slots => Set<Slot>();
public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Device>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.ApiKey).HasMaxLength(200).IsRequired();
        });

        b.Entity<Sensor>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.SensorCode }).IsUnique();
            e.Property(x => x.SensorCode).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Device)
              .WithMany(d => d.Sensors)
              .HasForeignKey(x => x.DeviceId);
        });

        b.Entity<Slot>(e =>
        {
            e.HasIndex(x => x.Label).IsUnique();
            e.Property(x => x.Label).HasMaxLength(20).IsRequired();
            e.Property(x => x.OccupiedThresholdCm).HasDefaultValue(15.0);

            e.HasOne(x => x.Sensor)
              .WithOne(s => s.Slot)
              .HasForeignKey<Slot>(x => x.SensorId)
              .OnDelete(DeleteBehavior.SetNull);
        });
        
b.Entity<TelemetryLog>(e =>
{
    e.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
    e.Property(x => x.SensorCode).HasMaxLength(20).IsRequired();
    e.Property(x => x.SlotLabel).HasMaxLength(20);

    e.HasIndex(x => x.ReceivedAtUtc);
    e.HasIndex(x => x.SlotLabel);
    e.HasIndex(x => x.DeviceCode);
});

        base.OnModelCreating(b);
    }
}

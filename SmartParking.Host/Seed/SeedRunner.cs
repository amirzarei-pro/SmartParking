using Microsoft.EntityFrameworkCore;
using SmartParking.Domain.Entities;
using SmartParking.Domain.Enums;
using SmartParking.Infrastructure;

namespace SmartParking.Host.Seed;

public static class SeedRunner
{
    public static async Task EnsureSeedAsync(SmartParkingDbContext db, CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);

        if (await db.Devices.AnyAsync(ct))
            return;

        var device = new Device
        {
            Code = "NODE-001",
            ApiKey = "DEV-KEY-001"
        };

        var s1 = new Sensor { Device = device, SensorCode = "S1" };
        var s2 = new Sensor { Device = device, SensorCode = "S2" };

        var a1 = new Slot { Label = "A1", Zone = "A", Sensor = s1, Status = SlotStatus.Offline };
        var a2 = new Slot { Label = "A2", Zone = "A", Sensor = s2, Status = SlotStatus.Offline };

        db.AddRange(device, s1, s2, a1, a2);
        await db.SaveChangesAsync(ct);
    }
}

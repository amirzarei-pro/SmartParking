using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartParking.Application.Contracts;
using SmartParking.Domain.Enums;
using SmartParking.Host.Hubs;
using SmartParking.Infrastructure;

namespace SmartParking.Host.Services;

public sealed class OfflineOptions
{
    public int TimeoutSeconds { get; set; } = 30;
    public int CheckIntervalSeconds { get; set; } = 5;
}

public sealed class OfflineMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ParkingHub> _hub;
    private readonly OfflineOptions _opt;

    public OfflineMonitor(
        IServiceScopeFactory scopeFactory,
        IHubContext<ParkingHub> hub,
        IOptions<OfflineOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_opt.CheckIntervalSeconds), stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();

            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_opt.TimeoutSeconds);

            // Slot هایی که Sensor دارند ولی سنسور مدت طولانی دیده نشده
            var slots = await db.Slots
                .Include(s => s.Sensor)
                .Where(s => s.SensorId != null && s.Sensor != null)
                .ToListAsync(stoppingToken);

            var changedSlots = new List<(string label, double distance, DateTimeOffset updatedAt)>();

            foreach (var slot in slots)
            {
                var lastSeen = slot.Sensor!.LastSeenAt;

                if (lastSeen < cutoff && slot.Status != SlotStatus.Offline)
                {
                    slot.Status = SlotStatus.Offline;
                    slot.LastUpdateAt = DateTimeOffset.UtcNow; // زمان تغییر وضعیت
                    changedSlots.Add((slot.Label, slot.LastDistanceCm, slot.LastUpdateAt));
                }
            }

            if (changedSlots.Count == 0)
                continue;

            await db.SaveChangesAsync(stoppingToken);

            // Push به UI
            foreach (var c in changedSlots)
            {
                var msg = new TelemetryIngestResultDto(
                    Updated: true,
                    SlotLabel: c.label,
                    Status: SlotStatus.Offline.ToString(),
                    DistanceCm: c.distance,
                    UpdatedAt: c.updatedAt
                );

                await _hub.Clients.All.SendAsync("slotUpdated", msg, stoppingToken);
            }
        }
    }
}

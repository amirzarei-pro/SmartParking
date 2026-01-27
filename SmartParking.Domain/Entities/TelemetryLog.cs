using SmartParking.Domain.Enums;

namespace SmartParking.Domain.Entities;

public class TelemetryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Snapshot fields (keep it simple; no heavy joins needed)
    public string DeviceCode { get; set; } = default!;
    public string SensorCode { get; set; } = default!;
    public string? SlotLabel { get; set; }

    public double DistanceCm { get; set; }
    public SlotStatus StatusAfter { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeviceTs { get; set; } // optional: if device sends timestamp
}

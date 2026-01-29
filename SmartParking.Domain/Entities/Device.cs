namespace SmartParking.Domain.Entities;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;      // NODE-001
    public string ApiKey { get; set; } = default!;   
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.MinValue;

    public List<Sensor> Sensors { get; set; } = new();
}

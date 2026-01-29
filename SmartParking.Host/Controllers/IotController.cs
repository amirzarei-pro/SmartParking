using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;
using SmartParking.Host.Hubs;

namespace SmartParking.Host.Controllers;

[ApiController]
[Route("api/iot")]
public sealed class IotController : ControllerBase
{
    private readonly IIotService _iot;
    private readonly IHubContext<ParkingHub> _hub;

    public IotController(IIotService iot, IHubContext<ParkingHub> hub)
    {
        _iot = iot;
        _hub = hub;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Telemetry([FromBody] TelemetryIngestDto dto, CancellationToken ct)
    {
        var key = Request.Headers["X-Device-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
            return Unauthorized("Missing X-Device-Key.");

        try
        {
            var result = await _iot.IngestAsync(dto, key, ct);
            
                await _hub.Clients.All.SendAsync("slotUpdated", result, ct);
            

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });


    [HttpPost("boot")]
    public async Task<IActionResult> Boot([FromBody] DeviceRegisterDto dto, CancellationToken ct)
    {
        var key = Request.Headers["X-Device-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
           return Unauthorized("Missing X-Device-Key.");

        try
        {
            var result = await _iot.RegisterSensorsAsync(dto, key, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

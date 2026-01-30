# UniSmartParking

English | [فارسی](README.fa.md)

A full-stack smart parking system with real-time slot monitoring powered by ESP8266 sensors, an ASP.NET Core host with SignalR, and a web dashboard. Includes a device simulator and Docker packaging for production.

## Overview
- Real-time parking slot status: Free, Occupied, Offline
- IoT device boot/registration and continuous telemetry ingestion
- Live dashboard with SignalR updates
- Admin login with cookie auth
- Offline monitor service with configurable thresholds
- Docker images (multi-stage) and Compose setup

## Architecture
- SmartParking.Host: ASP.NET Core server (Razor Components + SignalR + Web API)
- SmartParking.Application: Services + DTO contracts for boot/telemetry
- SmartParking.Domain: Entities (`Device`, `Sensor`, `Slot`) + `SlotStatus`
- SmartParking.Infrastructure: EF Core `SmartParkingDbContext` + migrations
- SmartParking.DeviceSim: Console simulator for testing telemetry
- IotFirmware/parking-esp8266: ESP8266 firmware sending boot + telemetry

SignalR hub: `/hubs/parking`
REST APIs (key header `X-Device-Key` required):
- `POST /api/iot/boot` – register device + sensors
- `POST /api/iot/telemetry` – send distance telemetry
- `GET /api/iot/ping` – health ping

## Quickstart
### Run locally (dev)
1. Ensure SQL Server is accessible and add a connection string under `SmartParking.Host/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "SmartParkingDb": "Server=localhost;Database=SmartParking_DB;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
     }
   }
   ```
2. Seed runs automatically on startup.
3. Run the host:
   ```bash
   dotnet run --project SmartParking.Host --urls "http://0.0.0.0:5294"
   ```
4. Open the dashboard at http://localhost:5294

### Default credentials
- Username: `admin`
- Password: `admin123`
(Override via env: `Auth__Username`, `Auth__Password`)

### Docker (production)
- Compose file: `docker/docker-compose.yml`
- Build + run:
  ```bash
  docker compose -f docker/docker-compose.yml up --build -d
  ```
- Exposes host on `http://localhost:5294` (mapped to container `8080`).
- Key envs:
  - `ConnectionStrings__SmartParkingDb`
  - `Auth__Username`, `Auth__Password`
  - `Offline__TimeoutSeconds`, `Offline__CheckIntervalSeconds`

## Configuration
- EF Core SQL Server connection: `SmartParkingDb`
- Offline monitor: `Offline` section
- SignalR hub path: `/hubs/parking`

## API Contracts
### Boot request (DeviceRegisterDto)
```json
{
  "deviceCode": "NODE-001",
  "sensors": [
    {
      "sensorCode": "S1",
      "slot": { "label": "A1", "zone": "A", "status": "Free", "occupiedThresholdCm": 15.0 }
    },
    {
      "sensorCode": "S2",
      "slot": { "label": "A2", "zone": "A", "status": "Free", "occupiedThresholdCm": 15.0 }
    }
  ]
}
```
Headers: `X-Device-Key: <device-api-key>`

Boot response (DeviceConnectResultDto):
```json
{
  "deviceId": "...",
  "deviceCode": "NODE-001",
  "sensorCount": 2,
  "sensors": [
    {
      "sensorId": "...",
      "sensorCode": "S1",
      "slot": { "slotId": "...", "label": "A1", "zone": "A", "status": "Free", "occupiedThresholdCm": 15.0 }
    }
  ]
}
```

### Telemetry request (TelemetryIngestDto)
```json
{
  "deviceCode": "NODE-001",
  "sensorCode": "S1",
  "distanceCm": 12.4,
  "ts": "2025-12-31T23:59:59Z"
}
```
Headers: `X-Device-Key: <device-api-key>`

Telemetry response (TelemetryIngestResultDto):
```json
{
  "updated": true,
  "slotLabel": "A1",
  "status": "Occupied",
  "distanceCm": 12.4,
  "updatedAt": "2026-01-30T10:00:00Z"
}
```

### Telemetry log (TelemetryLogDto) example
```json
{
  "deviceCode": "NODE-001",
  "sensorCode": "S1",
  "slotLabel": "A1",
  "distanceCm": 12.4,
  "statusAfter": "Occupied",
  "receivedAtUtc": "2026-01-30T10:00:00Z",
  "deviceTs": "2026-01-30T09:59:59Z"
}
```

## Device Simulator
Configure `SmartParking.DeviceSim/Program.cs`:
- `ServerBase`: e.g. `http://localhost:5294`
- `DeviceCode`: e.g. `NODE-002`
- `DeviceKey`: device API key stored in DB
Run:
```bash
dotnet run --project SmartParking.DeviceSim
```
Interactively set desired states; simulator sends boot + telemetry.

## Firmware (ESP8266)
See `IotFirmware/parking-esp8266` for full instructions and pin mappings. Firmware uses:
- Libraries: ESP8266WiFi, ESP8266HTTPClient, ArduinoJson, Adafruit_GFX, Adafruit_SSD1306, Wire
- Pins (example): `S1: trig D7, echo D8`, `S2: trig D5, echo D6`, buzzer `D3`
- Config: WiFi SSID/PASS, `SERVER_BASE`, `DEVICE_CODE`, `DEVICE_KEY`, telemetry interval, thresholds

## Development Notes
- ASP.NET Core 10.0 preview base images used in Dockerfile
- Database seeding runs on startup; errors are logged and app continues
- SignalR clients listen to `slotUpdated` messages

## Troubleshooting
- Unauthorized: ensure `X-Device-Key` header matches device API key
- No updates: check sensor slot mapping during boot
- ESP8266: use reachable IP for `SERVER_BASE` (not `localhost`), and verify WiFi credentials

## License
Not specified.

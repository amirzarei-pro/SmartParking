# ESP8266 Parking Firmware

English | [فارسی](README.fa.md)

Firmware for an ESP8266-based smart parking sensor node with ultrasonic sensors, OLED display (SSD1306), and buzzer. It performs device boot/mapping and sends telemetry distances to the UniSmartParking host.

## Hardware
- Board: ESP8266 (e.g., NodeMCU/WeMos D1 Mini)
- Ultrasonic sensors: HC-SR04 (x2 in sample)
- OLED: SSD1306 I2C (128x64)
- Buzzer: Active buzzer

### Pin mapping (sample)
- Sensor S1: `TRIG D7`, `ECHO D8`
- Sensor S2: `TRIG D5`, `ECHO D6`
- Buzzer: `D3`
- OLED: I2C (`SDA`, `SCL`)

## Software prerequisites
- Arduino IDE or PlatformIO
- Libraries:
  - ESP8266WiFi
  - ESP8266HTTPClient
  - ArduinoJson
  - Adafruit_GFX
  - Adafruit_SSD1306
  - Wire

## Configuration (top of source)
Edit `parking-esp8266.ino`:
```cpp
// WiFi
const char* WIFI_SSID = "<your-ssid>";
const char* WIFI_PASSWORD = "<your-pass>";

// Server reachable from ESP8266
const char* SERVER_BASE = "http://<host-ip>:5294"; // e.g., http://192.168.1.10:5294

// Device identity
const char* DEVICE_CODE = "NODE-001";
const char* DEVICE_KEY  = "<api-key-from-db>";

// Telemetry
const unsigned long TELEMETRY_INTERVAL_MS = 1000;
const unsigned long WIFI_CONNECT_TIMEOUT_MS = 15000;
```
Notes:
- Use a LAN-reachable IP for `SERVER_BASE` (do not use `localhost`).
- `DEVICE_KEY` must match your record in the DB. Boot/telemetry will be rejected without it.

## Boot payload
Endpoint: `POST /api/iot/boot`
Header: `X-Device-Key: <DEVICE_KEY>`
Example JSON sent by firmware:
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
The server responds with assigned IDs and current slot mapping.

## Telemetry payload
Endpoint: `POST /api/iot/telemetry`
Header: `X-Device-Key: <DEVICE_KEY>`
Example JSON:
```json
{
  "deviceCode": "NODE-001",
  "sensorCode": "S1",
  "distanceCm": 12.4,
  "ts": "2026-01-30T10:00:00Z"
}
```
The server responds with slot update result and current status.

## UI feedback
- OLED displays WiFi, boot status, last HTTP outcome, and per-sensor status
- Buzzer beeps:
  - Free: 1 short beep
  - Occupied: 2 short beeps
  - Offline: 3 short beeps

## Build & flash
1. Install libraries listed above
2. Select your ESP8266 board in Arduino IDE
3. Configure credentials and server IP
4. Compile and upload

## Troubleshooting
- WiFi fails: check SSID/PASS and 2.4GHz network availability
- Unauthorized: ensure `X-Device-Key` matches the DB-stored device key
- No updates: verify slot mapping during boot (slot should not be null)
- Distances noisy: stabilize sensor mount, verify power, adjust `occupiedThresholdCm`

## License
Copyright (c) 2026 Amir Zarei.

For licensing terms and permissions, see [LICENSE](../../LICENSE) or https://github.com/amirzarei-pro.

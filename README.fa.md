# یونی‌اسمارت‌پارکینگ

[English](README.md) | فارسی

سامانهٔ هوشمند پارکینگ با پایش لحظه‌ای وضعیت جای‌پارک‌ها (آزاد/اشغال/آفلاین) مبتنی بر سنسورهای ESP8266، میزبان ASP.NET Core با SignalR و داشبورد وب. شامل شبیه‌ساز دستگاه و بسته‌بندی داکر برای استقرار.

## نمای کلی
- وضعیت لحظه‌ای جای‌پارک: آزاد، اشغال، آفلاین
- راه‌اندازی/ثبت دستگاه و سنسورها + دریافت تله‌متری
- داشبورد زنده با بروزرسانی‌های SignalR
- ورود مدیر با احراز هویت کوکی
- سرویس مانیتور آفلاین با تنظیم‌پذیری
- ایمیج‌های داکر و فایل Compose

## معماری
- SmartParking.Host: سرور ASP.NET Core (Razor Components + SignalR + Web API)
- SmartParking.Application: سرویس‌ها و DTO ها برای بوت/تله‌متری
- SmartParking.Domain: انتیتی‌ها (`Device`, `Sensor`, `Slot`) + `SlotStatus`
- SmartParking.Infrastructure: EF Core و `SmartParkingDbContext` + مهاجرت‌ها
- SmartParking.DeviceSim: شبیه‌ساز کنسولی برای تست تله‌متری
- IotFirmware/parking-esp8266: فریمور ESP8266 برای ارسال بوت و تله‌متری

هاب SignalR: `/hubs/parking`
رست API ها (هدر `X-Device-Key` الزامی):
- `POST /api/iot/boot` – ثبت دستگاه و سنسورها
- `POST /api/iot/telemetry` – ارسال فاصله (تله‌متری)
- `GET /api/iot/ping` – بررسی سلامت

## شروع سریع
### اجرای محلی (توسعه)
1. اتصال SQL Server را آماده و کانکشن‌استرینگ را در `SmartParking.Host/appsettings.Development.json` قرار دهید:
   ```json
   {
     "ConnectionStrings": {
       "SmartParkingDb": "Server=localhost;Database=SmartParking_DB;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
     }
   }
   ```
2. سید دیتابیس در شروع برنامه به‌صورت خودکار اجرا می‌شود.
3. اجرای میزبان:
   ```bash
   dotnet run --project SmartParking.Host --urls "http://0.0.0.0:5294"
   ```
4. داشبورد: http://localhost:5294

### اطلاعات ورود پیش‌فرض
- نام کاربری: `admin`
- رمز عبور: `admin123`
(قابل تنظیم با محیط‌ها: `Auth__Username`, `Auth__Password`)

### داکر (تولید)
- فایل Compose: `docker/docker-compose.yml`
- ساخت و اجرا:
  ```bash
  docker compose -f docker/docker-compose.yml up --build -d
  ```
- دسترسی از `http://localhost:5294` (نگاشت به پورت داخلی `8080`).
- محیط‌های مهم:
  - `ConnectionStrings__SmartParkingDb`
  - `Auth__Username`, `Auth__Password`
  - `Offline__TimeoutSeconds`, `Offline__CheckIntervalSeconds`

## تنظیمات
- اتصال EF Core به SQL Server: `SmartParkingDb`
- گزینه‌های مانیتور آفلاین: سکشن `Offline`
- مسیر هاب SignalR: `/hubs/parking`

## قراردادهای API
### درخواست بوت (DeviceRegisterDto)
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
هدر: `X-Device-Key: <کلید-دستگاه>`

پاسخ بوت (DeviceConnectResultDto):
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

### درخواست تله‌متری (TelemetryIngestDto)
```json
{
  "deviceCode": "NODE-001",
  "sensorCode": "S1",
  "distanceCm": 12.4,
  "ts": "2025-12-31T23:59:59Z"
}
```
هدر: `X-Device-Key: <کلید-دستگاه>`

پاسخ تله‌متری (TelemetryIngestResultDto):
```json
{
  "updated": true,
  "slotLabel": "A1",
  "status": "Occupied",
  "distanceCm": 12.4,
  "updatedAt": "2026-01-30T10:00:00Z"
}
```

### نمونه لاگ تله‌متری (TelemetryLogDto)
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

## شبیه‌ساز دستگاه
تنظیمات در `SmartParking.DeviceSim/Program.cs`:
- `ServerBase`: مثل `http://localhost:5294`
- `DeviceCode`: مثل `NODE-002`
- `DeviceKey`: کلید API دستگاه در دیتابیس
اجرا:
```bash
dotnet run --project SmartParking.DeviceSim
```
حالت‌های موردنیاز را وارد کنید؛ بوت و تله‌متری ارسال می‌شود.

## فریمور (ESP8266)
به پوشه `IotFirmware/parking-esp8266` مراجعه کنید. کتابخانه‌ها:
- ESP8266WiFi, ESP8266HTTPClient, ArduinoJson, Adafruit_GFX, Adafruit_SSD1306, Wire
پین‌ها (نمونه): `S1: trig D7, echo D8`، `S2: trig D5, echo D6`، بازر `D3`
تنظیمات: WiFi، `SERVER_BASE`، `DEVICE_CODE`، `DEVICE_KEY`، بازهٔ تله‌متری و آستانه‌ها.

## نکات توسعه
- استفاده از ایمیج‌های پیش‌نمایش .NET 10 در Dockerfile
- سید دیتابیس هنگام شروع؛ در صورت خطا برنامه ادامه می‌دهد
- SignalR: پیام `slotUpdated` برای کلاینت‌ها

## رفع اشکال
- Unauthorized: هدر `X-Device-Key` را بررسی کنید
- عدم بروزرسانی: نگاشت سنسور به اسلات را در بوت بررسی کنید
- ESP8266: از IP قابل دسترس به‌جای `localhost` برای `SERVER_BASE` استفاده کنید و WiFi را بررسی کنید

## مجوز
مشخص نشده است.

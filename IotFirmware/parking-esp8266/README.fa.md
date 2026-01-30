# فریمور پارکینگ ESP8266

[English](README.md) | فارسی

فریمور برای نود سنسور پارکینگ مبتنی بر ESP8266 با سنسور اولتراسونیک، نمایشگر OLED (SSD1306) و بازر. عملیات بوت/نگاشت دستگاه را انجام می‌دهد و تله‌متری فاصله‌ها را به میزبان UniSmartParking ارسال می‌کند.

## سخت‌افزار
- برد: ESP8266 (مثل NodeMCU/WeMos D1 Mini)
- سنسورهای اولتراسونیک: HC-SR04 (نمونه شامل ۲ عدد)
- OLED: SSD1306 I2C (128x64)
- بازر: نوع فعال

### نگاشت پایه‌ها (نمونه)
- سنسور S1: `TRIG D7`، `ECHO D8`
- سنسور S2: `TRIG D5`، `ECHO D6`
- بازر: `D3`
- OLED: I2C (`SDA`, `SCL`)

## پیش‌نیازهای نرم‌افزاری
- Arduino IDE یا PlatformIO
- کتابخانه‌ها:
  - ESP8266WiFi
  - ESP8266HTTPClient
  - ArduinoJson
  - Adafruit_GFX
  - Adafruit_SSD1306
  - Wire

## تنظیمات (ابتدای سورس)
فایل `parking-esp8266.ino` را ویرایش کنید:
```cpp
// WiFi
const char* WIFI_SSID = "<ssid>";
const char* WIFI_PASSWORD = "<pass>";

// سرور قابل دسترس از ESP8266
const char* SERVER_BASE = "http://<host-ip>:5294"; // مثل: http://192.168.1.10:5294

// هویت دستگاه
const char* DEVICE_CODE = "NODE-001";
const char* DEVICE_KEY  = "<api-key-from-db>";

// تله‌متری
const unsigned long TELEMETRY_INTERVAL_MS = 1000;
const unsigned long WIFI_CONNECT_TIMEOUT_MS = 15000;
```
نکات:
- برای `SERVER_BASE` از IP قابل دسترس در شبکه استفاده کنید و از `localhost` استفاده نکنید.
- `DEVICE_KEY` باید با مقدار ذخیره شده در دیتابیس یکسان باشد، در غیر اینصورت بوت/تله‌متری رد می‌شود.

## payload بوت
اندپوینت: `POST /api/iot/boot`
هدر: `X-Device-Key: <DEVICE_KEY>`
نمونه JSON ارسال‌شده:
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
پاسخ شامل شناسه‌ها و نگاشت فعلی اسلات‌ها خواهد بود.

## payload تله‌متری
اندپوینت: `POST /api/iot/telemetry`
هدر: `X-Device-Key: <DEVICE_KEY>`
نمونه JSON:
```json
{
  "deviceCode": "NODE-001",
  "sensorCode": "S1",
  "distanceCm": 12.4,
  "ts": "2026-01-30T10:00:00Z"
}
```
پاسخ شامل نتیجهٔ بروزرسانی اسلات و وضعیت فعلی است.

## بازخورد رابط کاربری
- OLED وضعیت WiFi، بوت، آخرین نتیجهٔ HTTP و وضعیت سنسورها را نشان می‌دهد
- الگوی بوق:
  - آزاد: ۱ بوق کوتاه
  - اشغال: ۲ بوق کوتاه
  - آفلاین: ۳ بوق کوتاه

## ساخت و فلش
1. کتابخانه‌های بالا را نصب کنید
2. برد ESP8266 را در Arduino IDE انتخاب کنید
3. تنظیمات شبکه و IP سرور را اعمال کنید
4. کامپایل و آپلود کنید

## رفع اشکال
- اتصال WiFi: SSID/PASS و شبکه 2.4GHz را بررسی کنید
- Unauthorized: هدر `X-Device-Key` باید با کلید دستگاه در دیتابیس مطابق باشد
- عدم بروزرسانی: نگاشت اسلات در بوت را بررسی کنید (اسلات نباید null باشد)
- نویز فاصله: نصب سنسور را پایدار کنید، تغذیه را بررسی کنید و `occupiedThresholdCm` را تنظیم کنید

## مجوز
مشخص نشده است.

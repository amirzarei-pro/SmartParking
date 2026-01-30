#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClient.h>

#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#include <ArduinoJson.h>

// ===================== CONFIG =====================

// WiFi
const char* WIFI_SSID = "creator";
const char* WIFI_PASSWORD = "amiramir";

// Server (IMPORTANT: use IP or domain reachable from ESP8266; NOT localhost)
const char* SERVER_BASE = "http://10.114.52.107:5294";

// Device identity
const char* DEVICE_CODE = "NODE-001";
const char* DEVICE_KEY = "DEV-KEY-001";

// Telemetry interval
const unsigned long TELEMETRY_INTERVAL_MS = 1000;
const unsigned long WIFI_CONNECT_TIMEOUT_MS = 15000;

// OLED (SSD1306 I2C)
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET -1
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

// Buzzer (Active buzzer recommended)
const int BUZZER_PIN = D3;


// ===================== Ultrasonic / Models =====================

struct SlotInfo {
  bool mapped;                 // slot != null
  String label;                // "A1"
  String zone;                 // "A"
  String statusInit;           // "Free"
  double occupiedThresholdCm;  // 15.0
};

struct SensorConfig {
  const char* sensorCode;
  int trigPin;
  int echoPin;

  // Boot mapping (request)
  SlotInfo slot;

  // Runtime state
  double lastDistanceCm;
  String lastStatus;  // from telemetry response: "Free" / "Occupied" / "Offline"
};

SensorConfig sensors[] = {
  { "S1", D7, D8, { true, "A1", "A", "Free", 15.0 }, -1, "" },
  { "S2", D5, D6, { true, "A2", "A", "Free", 15.0 }, -1, "" }
};

const int SENSOR_COUNT = sizeof(sensors) / sizeof(sensors[0]);

// UI state
String lastHttp = "-";
String lastBoot = "-";
unsigned long lastTelemetryMs = 0;


// ===================== SERIAL LOGGING =====================

// Set to false if you want to reduce logs
static const bool SERIAL_DEBUG = true;

// Print prefix with millis for easier debugging
void logPrefix() {
  if (!SERIAL_DEBUG) return;
  Serial.print("[");
  Serial.print(millis());
  Serial.print("] ");
}

void logLine(const String& msg) {
  if (!SERIAL_DEBUG) return;
  logPrefix();
  Serial.println(msg);
}

// Print large responses safely (truncate)
void logLong(const String& title, const String& payload, size_t maxLen = 600) {
  if (!SERIAL_DEBUG) return;
  logPrefix();
  Serial.print(title);
  Serial.print(" (len=");
  Serial.print(payload.length());
  Serial.println("):");

  if (payload.length() <= maxLen) {
    Serial.println(payload);
  } else {
    Serial.println(payload.substring(0, maxLen));
    Serial.println("... [TRUNCATED]");
  }
}

// ===================== BUZZER =====================

void buzzerOff() {
  digitalWrite(BUZZER_PIN, LOW);
}

void beepShort(int times) {
  for (int i = 0; i < times; i++) {
    digitalWrite(BUZZER_PIN, HIGH);
    delay(80);
    digitalWrite(BUZZER_PIN, LOW);
    delay(80);
  }
}

void beepOccupied() {
  beepShort(2);
}  // 2 beeps
void beepFree() {
  beepShort(1);
}  // 1 beep
void beepOffline() {
  beepShort(3);
}  // 3 beeps

void handleStatusChange(SensorConfig& s, const String& newStatus) {
  // Only beep for mapped sensors (slot != null)
  if (!s.slot.mapped) {
    if (s.lastStatus != newStatus && newStatus.length() > 0) {
      logLine(String("Status change (unmapped) ") + s.sensorCode + ": " + s.lastStatus + " -> " + newStatus);
    }
    s.lastStatus = newStatus;
    return;
  }

  if (newStatus.length() == 0) return;

  if (s.lastStatus.length() == 0) {
    // First assignment, no beep
    logLine(String("Initial status ") + s.sensorCode + " [" + s.slot.label + "]: " + newStatus);
    s.lastStatus = newStatus;
    return;
  }

  if (s.lastStatus != newStatus) {
    logLine(String("Status change ") + s.sensorCode + " [" + s.slot.label + "]: " + s.lastStatus + " -> " + newStatus);

    if (newStatus == "Occupied") beepOccupied();
    else if (newStatus == "Free") beepFree();
    else if (newStatus == "Offline") beepOffline();

    s.lastStatus = newStatus;
  }
}

// ===================== ULTRASONIC =====================

// Measure distance in cm; return -1 on timeout/invalid
double readDistanceCm(int trigPin, int echoPin) {
  digitalWrite(trigPin, LOW);
  delayMicroseconds(3);

  digitalWrite(trigPin, HIGH);
  delayMicroseconds(10);

  digitalWrite(trigPin, LOW);

  // timeout 30ms (~5m)
  unsigned long duration = pulseIn(echoPin, HIGH, 40000UL);
  if (duration == 0) return -1;

  // HC-SR04 formula: cm = us / 58.0
  double cm = (double)duration / 58.0;

  // Round to 2 decimals
  cm = ((int)(cm * 100.0 + 0.5)) / 100.0;

  return cm;
}

// Median of 3 reads (noise reduction)
double readDistanceMedian3(int trigPin, int echoPin) {
  double a = readDistanceCm(trigPin, echoPin);
  delay(20);
  double b = readDistanceCm(trigPin, echoPin);
  delay(20);
  double c = readDistanceCm(trigPin, echoPin);

  if (a < 0 || b < 0 || c < 0) return -1;

  if ((a <= b && b <= c) || (c <= b && b <= a)) return b;
  if ((b <= a && a <= c) || (c <= a && a <= b)) return a;
  return c;
}

// ===================== OLED =====================

void oledRender() {
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);

  // Line 1
  display.setCursor(0, 0);
  display.print("WiFi:");
  display.print(WiFi.isConnected() ? "OK" : "DOWN");
  display.print(" RSSI:");
  display.print(WiFi.isConnected() ? WiFi.RSSI() : 0);

  // Line 2
  display.setCursor(0, 16);
  display.print("BOOT:");
  display.print(lastBoot);
  display.print(" HTTP:");
  display.print(lastHttp);

  // Sensor lines
  int y = 33;
  for (int i = 0; i < SENSOR_COUNT; i++) {
    display.setCursor(0, y);
    display.print(sensors[i].sensorCode);
    display.print(": ");

    if (sensors[i].lastDistanceCm < 0) display.print("--");
    else display.print(sensors[i].lastDistanceCm, 2);  // 2 decimals

    display.print("cm ");

    if (sensors[i].slot.mapped) {
      display.print("[");
      display.print(sensors[i].slot.label);
      display.print("]");
    } else {
      display.print("[-]");
    }

    y += 12;
  }

  display.display();
}

// ===================== WiFi =====================

void ensureWiFi() {
  if (WiFi.isConnected()) return;

  logLine(String("WiFi connect start. SSID=") + WIFI_SSID);

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long start = millis();
  while (!WiFi.isConnected() && (millis() - start < WIFI_CONNECT_TIMEOUT_MS)) {
    delay(250);
    lastHttp = "WiFi...";
    oledRender();
  }

  if (WiFi.isConnected()) {
    lastHttp = "WiFiOK";
    logLine(String("WiFi connected. IP=") + WiFi.localIP().toString() + " RSSI=" + String(WiFi.RSSI()));
  } else {
    lastHttp = "WiFiERR";
    logLine("WiFi connect FAILED (timeout).");
  }

  oledRender();
}

// ===================== HTTP =====================

bool httpPostJson(const String& url, const String& jsonBody, int& statusCodeOut, String& responseOut) {
  statusCodeOut = 0;
  responseOut = "";

  if (!WiFi.isConnected()) {
    logLine("HTTP POST skipped: WiFi not connected.");
    return false;
  }

  WiFiClient client;
  HTTPClient http;

  logLine(String("HTTP POST => ") + url);
  logLong("Request body", jsonBody, 600);

  if (!http.begin(client, url)) {
    logLine("HTTP begin() FAILED.");
    return false;
  }

  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Key", DEVICE_KEY);

  int code = http.POST(jsonBody);
  statusCodeOut = code;

  if (code > 0) responseOut = http.getString();

  http.end();

  lastHttp = String(code);

  logLine(String("HTTP status=") + code);
  if (responseOut.length() > 0) logLong("Response", responseOut, 600);

  return (code >= 200 && code < 300);
}

// ===================== API: BOOT (/api/iot/boot) =====================

bool sendBoot() {
  String url = String(SERVER_BASE) + "/api/iot/boot";

  StaticJsonDocument<768> doc;
  doc["deviceCode"] = DEVICE_CODE;

  JsonArray sensorsArr = doc.createNestedArray("sensors");

  for (int i = 0; i < SENSOR_COUNT; i++) {
    JsonObject s = sensorsArr.createNestedObject();
    s["sensorCode"] = sensors[i].sensorCode;

    if (sensors[i].slot.mapped) {
      JsonObject slot = s.createNestedObject("slot");
      slot["label"] = sensors[i].slot.label;
      slot["zone"] = sensors[i].slot.zone;
      slot["status"] = sensors[i].slot.statusInit;
      slot["occupiedThresholdCm"] = sensors[i].slot.occupiedThresholdCm;
    } else {
      s["slot"] = nullptr;  // Force JSON null
    }
  }

  String body;
  serializeJson(doc, body);

  logLine("Sending BOOT...");
  int code;
  String resp;
  bool ok = httpPostJson(url, body, code, resp);

  lastBoot = ok ? "OK" : "ERR";
  logLine(String("BOOT result=") + (ok ? "OK" : "ERR"));

  if (ok) beepShort(1);

  oledRender();
  return ok;
}

// ===================== API: TELEMETRY (/api/iot/telemetry) =====================

bool tryGetStatusFromTelemetryResponse(const String& resp, String& statusOut) {
  // 512 sometimes is not enough on ESP8266; use 1024 for safety.
  StaticJsonDocument<1024> doc;

  DeserializationError err = deserializeJson(doc, resp);
  if (err) {
    logLine(String("Telemetry JSON parse failed: ") + err.c_str());
    return false;
  }

  const char* status = doc["status"].as<const char*>();
  if (!status || status[0] == '\0')
    status = doc["Status"].as<const char*>();

  if (!status || status[0] == '\0')
    return false;

  statusOut = String(status);
  return true;
}


bool sendTelemetry(SensorConfig& s, double distanceCm) {
  if (distanceCm < 0) {
    logLine(String("Telemetry skip ") + s.sensorCode + ": invalid distance.");
    return false;
  }

  String url = String(SERVER_BASE) + "/api/iot/telemetry";

  StaticJsonDocument<256> doc;
  doc["deviceCode"] = DEVICE_CODE;
  doc["sensorCode"] = s.sensorCode;
  doc["distanceCm"] = distanceCm;
  doc["ts"] = nullptr;

  String body;
  serializeJson(doc, body);

  logLine(String("Sending Telemetry ") + s.sensorCode + " distance=" + String(distanceCm, 2));

  int code;
  String resp;
  bool ok = httpPostJson(url, body, code, resp);

  if (ok) {
    String newStatus;
    if (tryGetStatusFromTelemetryResponse(resp, newStatus)) {
      handleStatusChange(s, newStatus);
    } else {
      logLine(String("Telemetry ") + s.sensorCode + ": status not found (or parse failed).");
    }

  } else {
    logLine(String("Telemetry FAILED ") + s.sensorCode + " HTTP=" + String(code));
  }

  oledRender();
  return ok;
}

// ===================== SETUP / LOOP =====================

void setup() {
  Serial.begin(115200);
  delay(100);

  logLine("=== SmartParking Node START ===");
  logLine(String("DeviceCode=") + DEVICE_CODE + " Server=" + SERVER_BASE);

  pinMode(BUZZER_PIN, OUTPUT);
  buzzerOff();

  // Sensor pins
  for (int i = 0; i < SENSOR_COUNT; i++) {
    pinMode(sensors[i].trigPin, OUTPUT);
    pinMode(sensors[i].echoPin, INPUT);
    digitalWrite(sensors[i].trigPin, LOW);

    logLine(String("Sensor ") + sensors[i].sensorCode + " TRIG=" + sensors[i].trigPin + " ECHO=" + sensors[i].echoPin + " mapped=" + (sensors[i].slot.mapped ? "true" : "false") + (sensors[i].slot.mapped ? (String(" slot=") + sensors[i].slot.label) : ""));
  }

  // OLED init
  Wire.begin(D2, D1);  // SDA, SCL
  if (!display.begin(SSD1306_SWITCHCAPVCC, 0x3C)) {
    Serial.println("OLED init failed");
    logLine("OLED init FAILED (continuing without OLED).");
  } else {
    logLine("OLED init OK.");
  }

  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.println("SmartParking Node");
  display.println("Booting...");
  display.display();

  ensureWiFi();

  // Boot retry
  bool bootOk = false;
  for (int i = 0; i < 5; i++) {
    logLine(String("Boot attempt ") + (i + 1) + "/5");
    if (WiFi.isConnected() && sendBoot()) {
      bootOk = true;
      break;
    }
    delay(800);
    ensureWiFi();
  }

  if (!bootOk) {
    lastBoot = "ERR";
    logLine("BOOT failed after retries.");
    oledRender();
  }

  lastTelemetryMs = millis();
  logLine("Setup complete. Entering loop...");
}

void loop() {
  ensureWiFi();

  unsigned long now = millis();
  if (now - lastTelemetryMs >= TELEMETRY_INTERVAL_MS) {
    lastTelemetryMs = now;

    // Read distances
    for (int i = 0; i < SENSOR_COUNT; i++) {
      sensors[i].lastDistanceCm = readDistanceMedian3(sensors[i].trigPin, sensors[i].echoPin);

      if (sensors[i].lastDistanceCm < 0) {
        logLine(String("Read ") + sensors[i].sensorCode + ": timeout/invalid");
      } else {
        logLine(String("Read ") + sensors[i].sensorCode + ": " + String(sensors[i].lastDistanceCm, 2) + " cm");
      }
    }

    // Send telemetry for each sensor
    if (WiFi.isConnected()) {
      for (int i = 0; i < SENSOR_COUNT; i++) {
        sendTelemetry(sensors[i], sensors[i].lastDistanceCm);
        delay(80);
      }
    } else {
      lastHttp = "NO WIFI";
      logLine("Telemetry loop: NO WIFI");
      oledRender();
    }
  }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartParking.DeviceSim;

internal static class Program
{
    // ====== CONFIG (match your ESP source) ======
    private const string ServerBase = "http://127.0.0.1:5294";
    private const string DeviceCode = "NODE-002";          // simulated second device
    private const string DeviceKey  = "d0bba595641a45fc8977fdb85fda6b4d";       // set your key in DB
    private static readonly TimeSpan TelemetryInterval = TimeSpan.FromSeconds(1);

    // Sensors config (match your ESP structure)
    private static readonly SensorConfig[] Sensors =
    [
        new SensorConfig(
            sensorCode: "S1",
            slot: new SlotInfo(mapped: true, label: "C1", zone: "C", statusInit: "Free", occupiedThresholdCm: 15.0)
        ),
        new SensorConfig(
            sensorCode: "S2",
            slot: new SlotInfo(mapped: true, label: "C2", zone: "C", statusInit: "Free", occupiedThresholdCm: 15.0)
        ),
        new SensorConfig(
            sensorCode: "S3",
            slot: new SlotInfo(mapped: true, label: "C3", zone: "C", statusInit: "Free", occupiedThresholdCm: 15.0)
        ),
    ];

    // JSON options: keep nulls included (so ts:null and slot:null will be present if null)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly object StateLock = new();
    private static readonly Dictionary<string, DesiredMode> DesiredStates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("=== SmartParking Device Simulator (Console) ===");
        Console.WriteLine($"ServerBase : {ServerBase}");
        Console.WriteLine($"DeviceCode : {DeviceCode}");
        Console.WriteLine();

        foreach (var s in Sensors)
        {
            // Default: Free
            DesiredStates[s.SensorCode] = DesiredMode.Free;
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Add("X-Device-Key", DeviceKey);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // 1) Boot
        await SendBootAsync(http, cts.Token);

        // 2) Ask user initial desired states
        AskInitialStates();

        // 3) Run telemetry + command loop
        var telemetryTask = TelemetryLoopAsync(http, cts.Token);
        var commandTask = CommandLoopAsync(cts);

        await Task.WhenAny(telemetryTask, commandTask);
        cts.Cancel();

        try { await Task.WhenAll(telemetryTask, commandTask); }
        catch { /* ignore */ }

        Console.WriteLine("Stopped.");
    }

    private static void AskInitialStates()
    {
        Console.WriteLine();
        Console.WriteLine("Set initial slot states:");
        Console.WriteLine("  - Enter 'O' for Occupied, 'F' for Free, or a number for custom distance (cm).");
        Console.WriteLine("  - Example: S1=O  S2=F");
        Console.WriteLine();

        foreach (var s in Sensors)
        {
            if (!s.Slot.Mapped)
            {
                Console.WriteLine($"{s.SensorCode} is unmapped -> skipped");
                continue;
            }

            while (true)
            {
                Console.Write($"{s.SensorCode} [{s.Slot.Label}] (thr={s.Slot.OccupiedThresholdCm}): ");
                var input = (Console.ReadLine() ?? "").Trim();

                if (TryParseDesired(input, out var mode))
                {
                    lock (StateLock) DesiredStates[s.SensorCode] = mode;
                    break;
                }

                Console.WriteLine("Invalid. Use O / F / number (e.g. 12.64).");
            }
        }

        PrintStates();
        Console.WriteLine("Type 'help' for commands.");
        Console.WriteLine();
    }

    private static async Task CommandLoopAsync(CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (line is null) continue;

            line = line.Trim();
            if (line.Length == 0) continue;

            if (line.Equals("q", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                cts.Cancel();
                return;
            }

            if (line.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(@"
Commands:
  states
  set <SensorCode> <O|F|distance>   e.g. set S1 O   | set S2 12.64
  boot                             resend boot
  ping                             GET /api/iot/ping
  quit
");
                continue;
            }

            if (line.Equals("states", StringComparison.OrdinalIgnoreCase))
            {
                PrintStates();
                continue;
            }

            if (line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
            {
                // set S1 O
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    Console.WriteLine("Usage: set <SensorCode> <O|F|distance>");
                    continue;
                }

                var sensorCode = parts[1].Trim();
                var value = parts[2].Trim();

                if (!Sensors.Any(s => s.SensorCode.Equals(sensorCode, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("Unknown sensorCode.");
                    continue;
                }

                if (!TryParseDesired(value, out var mode))
                {
                    Console.WriteLine("Invalid value. Use O / F / number.");
                    continue;
                }

                lock (StateLock) DesiredStates[sensorCode] = mode;
                Console.WriteLine($"OK. {sensorCode} => {mode}");
                continue;
            }

            // Note: boot/ping require http, but command loop has no http reference; keep it simple:
            if (line.Equals("boot", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("ping", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Use Ctrl+C to stop, or run boot/ping by restarting program.");
                Console.WriteLine("(If you want, I can refactor command loop to call HTTP too.)");
                continue;
            }

            Console.WriteLine("Unknown command. Type 'help'.");
        }
    }

    private static async Task TelemetryLoopAsync(HttpClient http, CancellationToken ct)
    {
        var bootSent = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var s in Sensors)
                {
                    // If sensor is unmapped, still can send telemetry, but it won't map to a slot.
                    var desired = GetDesired(s.SensorCode);
                    var distance = ComputeDistanceFromDesired(s, desired);

                    var telemetry = new TelemetryIngestDto(
                        DeviceCode: DeviceCode,
                        SensorCode: s.SensorCode,
                        DistanceCm: distance,
                        Ts: null
                    );

                    var url = $"{ServerBase}/api/iot/telemetry";
                    var body = JsonSerializer.Serialize(telemetry, JsonOptions);

                    Log($"HTTP POST => {url}");
                    LogLong("Request body", body);

                    using var content = new StringContent(body, Encoding.UTF8, "application/json");
                    using var resp = await http.PostAsync(url, content, ct);
                    var respText = await resp.Content.ReadAsStringAsync(ct);

                    Log($"HTTP status={(int)resp.StatusCode}");
                    LogLong("Response", respText);

                    if (resp.IsSuccessStatusCode)
                    {
                        var result = TryParseTelemetryResult(respText);
                        if (result is not null)
                        {
                            // Save last server status (for visibility)
                            s.LastServerStatus = result.Status;

                            // Print readable line
                            Console.WriteLine($"{NowTag()} {s.SensorCode} [{result.SlotLabel}] => status={result.Status}, distance={result.DistanceCm}, updated={result.Updated}");
                        }
                    }
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{NowTag()} ERROR: {ex.Message}");
                bootSent = false;
            }

            // If something went wrong, try to re-boot once
            if (!bootSent && !ct.IsCancellationRequested)
            {
                try
                {
                    await SendBootAsync(http, ct);
                    bootSent = true;
                }
                catch { /* ignore */ }
            }

            await Task.Delay(TelemetryInterval, ct);
        }
    }

    private static async Task SendBootAsync(HttpClient http, CancellationToken ct)
    {
        var url = $"{ServerBase}/api/iot/boot";

        // Build boot body EXACTLY like your DeviceRegisterDto example
        var boot = new DeviceRegisterDto(
            DeviceCode: DeviceCode,
            Sensors: Sensors.Select(s =>
                new DeviceRegisterSensorDto(
                    SensorCode: s.SensorCode,
                    Slot: s.Slot.Mapped
                        ? new DeviceRegisterSlotDto(
                            Label: s.Slot.Label,
                            Zone: s.Slot.Zone,
                            Status: s.Slot.StatusInit,
                            OccupiedThresholdCm: s.Slot.OccupiedThresholdCm
                        )
                        : null
                )
            ).ToList()
        );

        var body = JsonSerializer.Serialize(boot, JsonOptions);

        Log($"HTTP POST => {url}");
        LogLong("Request body", body);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(url, content, ct);
        var respText = await resp.Content.ReadAsStringAsync(ct);

        Log($"HTTP status={(int)resp.StatusCode}");
        LogLong("Response", respText);

        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"{NowTag()} BOOT OK"
            : $"{NowTag()} BOOT ERR ({(int)resp.StatusCode})");
    }

    private static DesiredMode GetDesired(string sensorCode)
    {
        lock (StateLock)
        {
            if (DesiredStates.TryGetValue(sensorCode, out var m))
                return m;
            return DesiredMode.Free;
        }
    }

    private static double ComputeDistanceFromDesired(SensorConfig s, DesiredMode desired)
    {
        // If custom distance was set, use it
        if (desired.Kind == DesiredKind.CustomDistance)
            return Round2(desired.CustomDistanceCm);

        // For unmapped sensors, just simulate something safe
        var thr = s.Slot.Mapped ? s.Slot.OccupiedThresholdCm : 15.0;

        return desired.Kind switch
        {
            DesiredKind.Occupied => Round2(Math.Max(1.0, thr - 2.0)),
            DesiredKind.Free => Round2(thr + 10.0),
            _ => Round2(thr + 10.0)
        };
    }

    private static bool TryParseDesired(string input, out DesiredMode mode)
    {
        mode = DesiredMode.Free;

        input = input.Trim();
        if (input.Equals("O", StringComparison.OrdinalIgnoreCase) || input.Equals("Occupied", StringComparison.OrdinalIgnoreCase))
        {
            mode = DesiredMode.Occupied;
            return true;
        }

        if (input.Equals("F", StringComparison.OrdinalIgnoreCase) || input.Equals("Free", StringComparison.OrdinalIgnoreCase))
        {
            mode = DesiredMode.Free;
            return true;
        }

        if (double.TryParse(input, out var d))
        {
            mode = DesiredMode.Custom(d);
            return true;
        }

        return false;
    }

    private static TelemetryIngestResultDto? TryParseTelemetryResult(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TelemetryIngestResultDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static double Round2(double x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

    // ====== Logging helpers ======
    private static string NowTag() => $"[{DateTime.Now:HH:mm:ss}]";

    private static void Log(string msg) => Console.WriteLine($"{NowTag()} {msg}");

    private static void LogLong(string title, string payload, int maxLen = 600)
    {
        Console.WriteLine($"{NowTag()} {title} (len={payload.Length}):");
        if (payload.Length <= maxLen) Console.WriteLine(payload);
        else
        {
            Console.WriteLine(payload[..maxLen]);
            Console.WriteLine("... [TRUNCATED]");
        }
    }

    private static void PrintStates()
    {
        Console.WriteLine("Desired states:");
        lock (StateLock)
        {
            foreach (var s in Sensors)
            {
                var desired = DesiredStates.TryGetValue(s.SensorCode, out var m) ? m : DesiredMode.Free;
                var thr = s.Slot.Mapped ? s.Slot.OccupiedThresholdCm : 0;
                Console.WriteLine($"  {s.SensorCode} slot={(s.Slot.Mapped ? s.Slot.Label : "-")} thr={thr} => {desired}");
            }
        }
    }

    // ====== DTOs (match your API contracts) ======

    public sealed record TelemetryIngestDto(
        string DeviceCode,
        string SensorCode,
        double DistanceCm,
        DateTimeOffset? Ts
    );

    public sealed record TelemetryIngestResultDto(
        bool Updated,
        string? SlotLabel,
        string? Status,
        double? DistanceCm,
        DateTimeOffset? UpdatedAt
    );

    public sealed record DeviceRegisterDto(
        string DeviceCode,
        List<DeviceRegisterSensorDto> Sensors
    );

    public sealed record DeviceRegisterSensorDto(
        string SensorCode,
        DeviceRegisterSlotDto? Slot
    );

    public sealed record DeviceRegisterSlotDto(
        string Label,
        string Zone,
        string Status,
        double OccupiedThresholdCm
    );

    // ====== Models ======
    private sealed class SensorConfig
    {
        public string SensorCode { get; }
        public SlotInfo Slot { get; }
        public string? LastServerStatus { get; set; }

        public SensorConfig(string sensorCode, SlotInfo slot)
        {
            SensorCode = sensorCode;
            Slot = slot;
        }
    }

    private sealed class SlotInfo
    {
        public bool Mapped { get; }
        public string Label { get; }
        public string Zone { get; }
        public string StatusInit { get; }
        public double OccupiedThresholdCm { get; }

        public SlotInfo(bool mapped, string label, string zone, string statusInit, double occupiedThresholdCm)
        {
            Mapped = mapped;
            Label = label;
            Zone = zone;
            StatusInit = statusInit;
            OccupiedThresholdCm = occupiedThresholdCm;
        }
    }

    private enum DesiredKind
    {
        Free,
        Occupied,
        CustomDistance
    }

    private readonly struct DesiredMode
    {
        public DesiredKind Kind { get; }
        public double CustomDistanceCm { get; }

        private DesiredMode(DesiredKind kind, double customDistanceCm = 0)
        {
            Kind = kind;
            CustomDistanceCm = customDistanceCm;
        }

        public static DesiredMode Free => new(DesiredKind.Free);
        public static DesiredMode Occupied => new(DesiredKind.Occupied);
        public static DesiredMode Custom(double cm) => new(DesiredKind.CustomDistance, cm);

        public override string ToString()
            => Kind == DesiredKind.CustomDistance ? $"Custom({CustomDistanceCm:0.00}cm)" : Kind.ToString();
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenClawPanel;

public partial class Form1 : Form
{
    // --- .env Config ---
    static readonly string EnvFile = FindEnvFile();
    static readonly Dictionary<string, string> envVars = LoadEnv();

    static string FindEnvFile()
    {
        // Try project root first (for development), then exe directory
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return candidates[0];
    }

    static Dictionary<string, string> LoadEnv() => LoadEnvFile(FindEnvFile());

    static Dictionary<string, string> LoadEnvFile(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return dict;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            dict[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
        }
        return dict;
    }

    static string Env(string key, string fallback = "") =>
        envVars.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

    // --- Resolved Config (all values from .env, no hardcoded paths) ---
    static readonly string OPENCLAW_HOME = Env("OPENCLAW_HOME");
    static readonly string OPENCLAW_CONFIG = Env("OPENCLAW_CONFIG");
    static readonly string OPENCLAW_CLI = Env("OPENCLAW_CLI");
    static readonly string OPENCLAW_DOTENV = Env("OPENCLAW_DOTENV");
    static readonly string NGROK_EXE = Env("NGROK_EXE", "ngrok");
    static readonly string NGROK_POLICY = Env("NGROK_POLICY");
    static readonly int GATEWAY_PORT = int.TryParse(Env("GATEWAY_PORT", "18789"), out var p) ? p : 18789;
    static readonly string GATEWAY_URL = Env("GATEWAY_URL", $"http://127.0.0.1:{GATEWAY_PORT}");
    static readonly string NGROK_API = Env("NGROK_API", "http://127.0.0.1:4040/api/tunnels");

    // Gateway token loaded dynamically from openclaw.json
    static readonly string GATEWAY_TOKEN = LoadGatewayToken();

    static string LoadGatewayToken()
    {
        try
        {
            var json = File.ReadAllText(OPENCLAW_CONFIG);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("gateway")
                .GetProperty("auth")
                .GetProperty("token")
                .GetString() ?? "";
        }
        catch { return ""; }
    }

    // Known OpenClaw ports (Bridge removed)
    static readonly (int port, string service)[] KNOWN_PORTS =
    [
        (GATEWAY_PORT, "Gateway"),
        (GATEWAY_PORT + 2, "Gateway BrowserCtl"),
        (GATEWAY_PORT + 3, "Gateway Internal"),
        (4040, "ngrok API"),
    ];

    // Port scan ranges
    static readonly (int from, int to)[] SCAN_RANGES =
    [
        (GATEWAY_PORT - 9, GATEWAY_PORT + 11), // Gateway range
        (4030, 4050),                            // ngrok range
    ];

    // ANSI strip regex
    static readonly Regex AnsiRegex = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);
    static string StripAnsi(string s) => AnsiRegex.Replace(s, "");

    // --- UI Elements ---
    readonly Label lblTitle = new();

    // TabControl
    readonly TabControl tabControl = new();
    readonly TabPage tabControl_ = new("Control");
    readonly TabPage tabLogs = new("Logs");
    readonly TabPage tabConnection = new("Connection");

    // Control tab - Service rows
    readonly Panel pnlGatewayDot = new(), pnlNgrokDot = new();
    readonly Label lblGateway = new(), lblNgrok = new();
    readonly Button btnGwStart = new(), btnGwStop = new();
    readonly Button btnNgStart = new(), btnNgStop = new();
    readonly Label lblNgrokUrl = new();

    // Control tab - Global controls
    readonly Button btnStartAll = new(), btnStopAll = new();
    readonly Button btnWebView = new(), btnRefresh = new();

    // Control tab - Ports section
    readonly Label lblPortsHeader = new();
    readonly Button btnScan = new();
    readonly Label lblPortColHdr = new();
    readonly Panel pnlPorts = new();

    // Control tab - Footer
    readonly Label lblLastCheck = new();

    // Logs tab
    readonly RichTextBox rtbLog = new();
    readonly Button btnLogClear = new();
    readonly CheckBox chkAutoScroll = new();
    readonly Label lblLogFile = new();

    // Connection tab - Alert banner
    readonly Panel pnlAlert = new();
    readonly Label lblAlertText = new();
    readonly Button btnOpenLineConsole = new();
    readonly Button btnDismissAlert = new();

    // Connection tab - ngrok URL
    readonly Label lblNgrokUrlTitle = new();
    readonly TextBox txtNgrokUrl = new();
    readonly Button btnCopyNgrokUrl = new();

    // Connection tab - Webhook URLs
    readonly Label lblWebhookTitle = new();
    readonly TextBox txtLineWebhook = new();
    readonly Button btnCopyLineWebhook = new();
    readonly Label lblLineLabel = new();

    // Connection tab - Gateway Token
    readonly Label lblTokenTitle = new();
    readonly TextBox txtGatewayToken = new();
    readonly Button btnToggleToken = new();
    readonly Button btnCopyToken = new();

    // Connection tab - Gateway Status
    readonly Label lblStatusTitle = new();
    readonly Panel pnlGwStatusDot = new();
    readonly Label lblGwStatusInfo = new();

    // Timers and HTTP
    readonly System.Windows.Forms.Timer timer = new();
    readonly System.Windows.Forms.Timer logTimer = new();
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };

    // State
    bool gatewayUp, ngrokUp;
    string ngrokUrl = "";
    string lastKnownNgrokUrl = "";
    bool alertDismissed = false;
    List<PortEntry> activePorts = [];

    // Log tail state
    long lastLogReadPosition = 0;
    string currentLogDate = "";
    FileSystemWatcher? logWatcher;
    int logLineCount = 0;

    record PortEntry(int Port, string Service, int Pid, string ProcessName);

    // Webhook channel definitions (extensible)
    static readonly (string name, string suffix)[] WebhookChannels =
    [
        ("LINE", "/line/webhook"),
    ];

    public Form1()
    {
        InitializeComponent();
        SetupUI();

        // Main refresh timer (5s)
        timer.Interval = 5000;
        timer.Tick += async (_, _) => await RefreshStatus();
        timer.Start();

        // Log tail timer (2s fallback polling)
        logTimer.Interval = 2000;
        logTimer.Tick += (_, _) => PollLogFile();
        logTimer.Start();

        // Initial load
        _ = RefreshStatus();
        InitLogTail();
    }
}

using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace OpenClawPanel;

public partial class Form1 : Form
{
    // --- Config ---
    const string GATEWAY_URL = "http://127.0.0.1:18789";
    const string BRIDGE_URL = "http://127.0.0.1:3847";
    const string NGROK_API = "http://127.0.0.1:4040/api/tunnels";
    const string OPENCLAW_HOME = @"E:\OpenClawWorkSpace";
    const string OPENCLAW_CONFIG = @"E:\OpenClawWorkSpace\.openclaw\openclaw.json";
    const string BRIDGE_SCRIPT = @"E:\OpenClawWorkSpace\OpenClaw-AtomicMemory\scripts\openclaw-bridge-server.js";
    const string BRIDGE_TOKEN = "openclaw-bridge-default-token";
    const string NGROK_EXE = @"C:\Users\holyl\AppData\Local\Microsoft\WinGet\Packages\Ngrok.Ngrok_Microsoft.Winget.Source_8wekyb3d8bbwe\ngrok.exe";
    const string GATEWAY_TOKEN = "5036a91800f518d3c06541e3918c5ae59b42e5d9511b7346b48db0e7b435b609";
    const string NGROK_POLICY = @"E:\OpenClawWorkSpace\.openclaw\ngrok-policy.yml";

    // Known OpenClaw ports
    static readonly (int port, string service)[] KNOWN_PORTS =
    [
        (18789, "Gateway"),
        (18791, "Gateway BrowserCtl"),
        (18792, "Gateway Internal"),
        (3847, "Bridge"),
        (4040, "ngrok API"),
    ];

    // Port scan ranges (short segments around known ports)
    static readonly (int from, int to)[] SCAN_RANGES =
    [
        (18780, 18800),
        (3840, 3860),
        (4030, 4050),
    ];

    // --- UI Elements ---
    readonly Label lblTitle = new();

    // Service rows
    readonly Panel pnlGatewayDot = new(), pnlBridgeDot = new(), pnlNgrokDot = new();
    readonly Label lblGateway = new(), lblBridge = new(), lblNgrok = new();
    readonly Button btnGwStart = new(), btnGwStop = new();
    readonly Button btnBrStart = new(), btnBrStop = new();
    readonly Button btnNgStart = new(), btnNgStop = new();
    readonly Label lblNgrokUrl = new();

    // Global controls
    readonly Button btnStartAll = new(), btnStopAll = new();
    readonly Button btnDashboard = new(), btnRefresh = new();

    // Ports section
    readonly Label lblPortsHeader = new();
    readonly Button btnScan = new();
    readonly Label lblPortColHdr = new();
    readonly Panel pnlPorts = new();

    // Footer
    readonly Label lblLastCheck = new();

    readonly System.Windows.Forms.Timer timer = new();
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };

    bool gatewayUp, bridgeUp, ngrokUp;
    string ngrokUrl = "";
    List<PortEntry> activePorts = [];

    record PortEntry(int Port, string Service, int Pid, string ProcessName);

    public Form1()
    {
        InitializeComponent();
        SetupUI();
        timer.Interval = 5000;
        timer.Tick += async (_, _) => await RefreshStatus();
        timer.Start();
        _ = RefreshStatus();
    }

    void SetupUI()
    {
        Text = "OpenClaw Panel";
        Size = new Size(580, 550);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(24, 24, 32);
        Font = new Font("Segoe UI", 10f);

        // --- Title ---
        lblTitle.Text = "OpenClaw Control Panel";
        lblTitle.Font = new Font("Segoe UI Semibold", 16f);
        lblTitle.ForeColor = Color.FromArgb(200, 220, 255);
        lblTitle.Location = new Point(20, 16);
        lblTitle.AutoSize = true;
        Controls.Add(lblTitle);

        // --- Service Rows ---
        int y = 60;
        SetupServiceRow(pnlGatewayDot, lblGateway, "Gateway", ":18789",
            btnGwStart, btnGwStop, ref y);
        btnGwStart.Click += async (_, _) => await StartGateway();
        btnGwStop.Click += async (_, _) => await StopGateway();

        SetupServiceRow(pnlBridgeDot, lblBridge, "Bridge", ":3847",
            btnBrStart, btnBrStop, ref y);
        btnBrStart.Click += async (_, _) => await StartBridge();
        btnBrStop.Click += async (_, _) => await StopBridge();

        SetupServiceRow(pnlNgrokDot, lblNgrok, "ngrok", "tunnel",
            btnNgStart, btnNgStop, ref y);
        btnNgStart.Click += async (_, _) => await StartNgrok();
        btnNgStop.Click += async (_, _) => await StopNgrok();

        // ngrok URL (click to copy)
        lblNgrokUrl.Text = "";
        lblNgrokUrl.ForeColor = Color.FromArgb(120, 160, 200);
        lblNgrokUrl.Font = new Font("Consolas", 8.5f);
        lblNgrokUrl.Location = new Point(44, y);
        lblNgrokUrl.Size = new Size(420, 20);
        lblNgrokUrl.Cursor = Cursors.Hand;
        lblNgrokUrl.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(ngrokUrl))
                Clipboard.SetText(ngrokUrl);
        };
        Controls.Add(lblNgrokUrl);
        y += 28;

        // --- Global Buttons ---
        MakeButton(btnStartAll, "\u25b6 Start All", 14, y, 100, 36,
            Color.FromArgb(60, 220, 120), Color.FromArgb(30, 50, 40), Color.FromArgb(60, 180, 100));
        btnStartAll.Click += async (_, _) => await StartAll();

        MakeButton(btnStopAll, "\u25a0 Stop All", 120, y, 100, 36,
            Color.FromArgb(240, 100, 100), Color.FromArgb(50, 30, 30), Color.FromArgb(200, 80, 80));
        btnStopAll.Click += async (_, _) => await StopAll();

        MakeButton(btnDashboard, "\ud83c\udf10 Dashboard", 226, y, 120, 36,
            Color.FromArgb(230, 210, 100), Color.FromArgb(40, 38, 25), Color.FromArgb(180, 160, 60));
        btnDashboard.Click += (_, _) => OpenDashboard();

        MakeButton(btnRefresh, "\u21bb Refresh", 352, y, 100, 36,
            Color.FromArgb(140, 180, 240), Color.FromArgb(30, 35, 50), Color.FromArgb(100, 140, 200));
        btnRefresh.Click += async (_, _) => await RefreshStatus();
        y += 50;

        // --- Ports Section ---
        lblPortsHeader.Text = "\u2500\u2500\u2500 Active Ports \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500";
        lblPortsHeader.ForeColor = Color.FromArgb(100, 110, 140);
        lblPortsHeader.Font = new Font("Segoe UI", 9f);
        lblPortsHeader.Location = new Point(14, y);
        lblPortsHeader.AutoSize = true;
        Controls.Add(lblPortsHeader);

        MakeButton(btnScan, "\ud83d\udd0d Scan", 472, y - 4, 76, 26,
            Color.FromArgb(160, 180, 220), Color.FromArgb(30, 32, 45), Color.FromArgb(80, 100, 150));
        btnScan.Font = new Font("Segoe UI", 8.5f);
        btnScan.Click += async (_, _) => await RefreshStatus();
        y += 26;

        // Column headers
        lblPortColHdr.Text = "  Port    Service              PID      Process";
        lblPortColHdr.ForeColor = Color.FromArgb(100, 108, 130);
        lblPortColHdr.Font = new Font("Consolas", 8.5f);
        lblPortColHdr.Location = new Point(14, y);
        lblPortColHdr.AutoSize = true;
        Controls.Add(lblPortColHdr);
        y += 20;

        // Scrollable port list panel
        pnlPorts.Location = new Point(14, y);
        pnlPorts.Size = new Size(536, 170);
        pnlPorts.AutoScroll = true;
        pnlPorts.BackColor = Color.FromArgb(20, 20, 28);
        Controls.Add(pnlPorts);
        y += 178;

        // Last check
        lblLastCheck.Text = "";
        lblLastCheck.ForeColor = Color.FromArgb(90, 90, 110);
        lblLastCheck.Font = new Font("Segoe UI", 8f);
        lblLastCheck.Location = new Point(20, y);
        lblLastCheck.AutoSize = true;
        Controls.Add(lblLastCheck);
    }

    void SetupServiceRow(Panel dot, Label lbl, string name, string detail,
        Button btnStart, Button btnStop, ref int y)
    {
        // Status dot
        dot.Size = new Size(14, 14);
        dot.Location = new Point(24, y + 5);
        dot.BackColor = Color.FromArgb(80, 80, 80);
        MakeCircle(dot);
        Controls.Add(dot);

        // Service label
        lbl.Text = $"{name}  ({detail})  \u2014  checking...";
        lbl.ForeColor = Color.FromArgb(180, 180, 195);
        lbl.Location = new Point(48, y + 1);
        lbl.AutoSize = true;
        Controls.Add(lbl);

        // Individual Start button
        btnStart.Text = "\u25b6";
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.ForeColor = Color.FromArgb(60, 200, 100);
        btnStart.BackColor = Color.FromArgb(28, 40, 32);
        btnStart.FlatAppearance.BorderColor = Color.FromArgb(50, 140, 80);
        btnStart.Size = new Size(36, 26);
        btnStart.Location = new Point(440, y);
        btnStart.Cursor = Cursors.Hand;
        btnStart.Font = new Font("Segoe UI", 9f);
        Controls.Add(btnStart);

        // Individual Stop button
        btnStop.Text = "\u25a0";
        btnStop.FlatStyle = FlatStyle.Flat;
        btnStop.ForeColor = Color.FromArgb(220, 90, 90);
        btnStop.BackColor = Color.FromArgb(40, 28, 28);
        btnStop.FlatAppearance.BorderColor = Color.FromArgb(160, 70, 70);
        btnStop.Size = new Size(36, 26);
        btnStop.Location = new Point(482, y);
        btnStop.Cursor = Cursors.Hand;
        btnStop.Font = new Font("Segoe UI", 9f);
        Controls.Add(btnStop);

        y += 34;
    }

    void MakeButton(Button btn, string text, int x, int y, int w, int h,
        Color fore, Color back, Color border)
    {
        btn.Text = text;
        btn.FlatStyle = FlatStyle.Flat;
        btn.ForeColor = fore;
        btn.BackColor = back;
        btn.FlatAppearance.BorderColor = border;
        btn.Size = new Size(w, h);
        btn.Location = new Point(x, y);
        btn.Cursor = Cursors.Hand;
        Controls.Add(btn);
    }

    static void MakeCircle(Panel p)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(0, 0, p.Width, p.Height);
        p.Region = new Region(path);
    }

    void SetDot(Panel dot, bool up) =>
        dot.BackColor = up ? Color.FromArgb(60, 220, 120) : Color.FromArgb(200, 60, 60);

    // ===================== Status Check =====================

    async Task RefreshStatus()
    {
        // Fix: Gateway root returns 404, use /health endpoint
        gatewayUp = await CheckUrl(GATEWAY_URL + "/health");
        bridgeUp = await CheckUrl(BRIDGE_URL + "/health");
        (ngrokUp, ngrokUrl) = await CheckNgrok();
        activePorts = await ScanActivePorts();

        if (InvokeRequired)
            Invoke(UpdateUI);
        else
            UpdateUI();
    }

    void UpdateUI()
    {
        // Service dots
        SetDot(pnlGatewayDot, gatewayUp);
        SetDot(pnlBridgeDot, bridgeUp);
        SetDot(pnlNgrokDot, ngrokUp);

        // Service labels
        lblGateway.Text = $"Gateway  (:18789)  \u2014  {(gatewayUp ? "RUNNING" : "STOPPED")}";
        lblGateway.ForeColor = gatewayUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        lblBridge.Text = $"Bridge  (:3847)  \u2014  {(bridgeUp ? "RUNNING" : "STOPPED")}";
        lblBridge.ForeColor = bridgeUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        lblNgrok.Text = $"ngrok  (tunnel)  \u2014  {(ngrokUp ? "RUNNING" : "STOPPED")}";
        lblNgrok.ForeColor = ngrokUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        // Per-service button enable/disable
        btnGwStart.Enabled = !gatewayUp;
        btnGwStop.Enabled = gatewayUp;
        btnBrStart.Enabled = !bridgeUp;
        btnBrStop.Enabled = bridgeUp;
        btnNgStart.Enabled = !ngrokUp;
        btnNgStop.Enabled = ngrokUp;

        // ngrok URL
        lblNgrokUrl.Text = ngrokUp ? $"  {ngrokUrl}  (click to copy)" : "";

        // Timestamp
        lblLastCheck.Text = $"Last check: {DateTime.Now:HH:mm:ss}   (auto-refresh 5s)";

        // Port list
        UpdatePortsList();
    }

    void UpdatePortsList()
    {
        pnlPorts.SuspendLayout();
        pnlPorts.Controls.Clear();

        int y = 0;
        foreach (var entry in activePorts)
        {
            var lblPort = new Label
            {
                Text = entry.Port.ToString(),
                Font = new Font("Consolas", 9f),
                ForeColor = Color.FromArgb(180, 200, 220),
                Location = new Point(4, y + 4),
                Size = new Size(52, 20),
            };
            var lblSvc = new Label
            {
                Text = entry.Service,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = KNOWN_PORTS.Any(k => k.port == entry.Port)
                    ? Color.FromArgb(150, 180, 210)
                    : Color.FromArgb(200, 160, 100), // highlight unknown ports
                Location = new Point(62, y + 4),
                Size = new Size(160, 20),
            };
            var lblPid = new Label
            {
                Text = entry.Pid.ToString(),
                Font = new Font("Consolas", 8.5f),
                ForeColor = Color.FromArgb(130, 140, 160),
                Location = new Point(228, y + 4),
                Size = new Size(60, 20),
            };
            var lblProc = new Label
            {
                Text = entry.ProcessName,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 130, 150),
                Location = new Point(294, y + 4),
                Size = new Size(120, 20),
            };

            // Kill button (always available)
            var btnKill = new Button
            {
                Text = "Kill",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(240, 120, 120),
                BackColor = Color.FromArgb(45, 28, 28),
                Size = new Size(48, 24),
                Location = new Point(468, y + 1),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8f),
                Tag = entry.Port,
            };
            btnKill.FlatAppearance.BorderColor = Color.FromArgb(160, 70, 70);
            btnKill.Click += async (s, _) =>
            {
                var port = (int)((Button)s!).Tag!;
                KillByPort(port);
                await Task.Delay(1000);
                await RefreshStatus();
            };

            pnlPorts.Controls.AddRange([lblPort, lblSvc, lblPid, lblProc, btnKill]);
            y += 28;
        }

        if (activePorts.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "  No active ports detected in scan ranges",
                ForeColor = Color.FromArgb(80, 80, 100),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(4, 8),
                AutoSize = true,
            };
            pnlPorts.Controls.Add(lblEmpty);
        }

        pnlPorts.ResumeLayout();
    }

    async Task<bool> CheckUrl(string url)
    {
        try
        {
            var resp = await http.GetAsync(url);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    async Task<(bool up, string url)> CheckNgrok()
    {
        try
        {
            var resp = await http.GetStringAsync(NGROK_API);
            using var doc = JsonDocument.Parse(resp);
            var tunnels = doc.RootElement.GetProperty("tunnels");
            if (tunnels.GetArrayLength() > 0)
            {
                var url = tunnels[0].GetProperty("public_url").GetString() ?? "";
                return (true, url);
            }
        }
        catch { }
        return (false, "");
    }

    async Task<List<PortEntry>> ScanActivePorts()
    {
        var results = new List<PortEntry>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c netstat -ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var seen = new HashSet<int>();
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains("LISTENING")) continue;
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;

                var local = parts[1];
                var ci = local.LastIndexOf(':');
                if (ci < 0) continue;
                if (!int.TryParse(local[(ci + 1)..], out int port)) continue;
                if (!int.TryParse(parts[^1], out int pid)) continue;

                // Only include ports in known list or scan ranges
                bool hit = KNOWN_PORTS.Any(k => k.port == port)
                    || SCAN_RANGES.Any(r => port >= r.from && port <= r.to);
                if (!hit || !seen.Add(port)) continue;

                string svc = KNOWN_PORTS.FirstOrDefault(k => k.port == port).service ?? "\u2014";
                string pname = "";
                try { pname = Process.GetProcessById(pid).ProcessName; } catch { }

                results.Add(new PortEntry(port, svc, pid, pname));
            }
        }
        catch { }
        return results.OrderBy(p => p.Port).ToList();
    }

    // ===================== Individual Service Control =====================

    async Task StartGateway()
    {
        btnGwStart.Enabled = false;
        var env = new Dictionary<string, string>
        {
            ["OPENCLAW_HOME"] = OPENCLAW_HOME,
            ["OPENCLAW_CONFIG_PATH"] = OPENCLAW_CONFIG,
        };
        StartProcess("cmd.exe", "/c openclaw gateway --port 18789", env);
        await Task.Delay(3000);
        await RefreshStatus();
        btnGwStart.Enabled = true;
    }

    async Task StopGateway()
    {
        btnGwStop.Enabled = false;
        RunCmd("cmd.exe", "/c openclaw gateway stop");
        KillByPort(18789);
        await Task.Delay(2000);
        await RefreshStatus();
        btnGwStop.Enabled = true;
    }

    async Task StartBridge()
    {
        btnBrStart.Enabled = false;
        var env = new Dictionary<string, string>
        {
            ["BRIDGE_TOKEN"] = BRIDGE_TOKEN,
            ["OPENCLAW_HOME"] = OPENCLAW_HOME,
        };
        StartProcess("node", BRIDGE_SCRIPT, env);
        await Task.Delay(2000);
        await RefreshStatus();
        btnBrStart.Enabled = true;
    }

    async Task StopBridge()
    {
        btnBrStop.Enabled = false;
        KillNodeScript("bridge-server");
        await Task.Delay(1500);
        await RefreshStatus();
        btnBrStop.Enabled = true;
    }

    async Task StartNgrok()
    {
        btnNgStart.Enabled = false;
        // Fix: include traffic-policy-file for Bearer token injection
        StartProcess(NGROK_EXE, $"http 18789 --traffic-policy-file=\"{NGROK_POLICY}\"");
        await Task.Delay(5000);
        await RefreshStatus();
        btnNgStart.Enabled = true;
    }

    async Task StopNgrok()
    {
        btnNgStop.Enabled = false;
        KillByName("ngrok");
        await Task.Delay(1500);
        await RefreshStatus();
        btnNgStop.Enabled = true;
    }

    // ===================== Start All / Stop All =====================

    async Task StartAll()
    {
        btnStartAll.Enabled = false;
        btnStartAll.Text = "Starting...";

        if (!gatewayUp)
        {
            var env = new Dictionary<string, string>
            {
                ["OPENCLAW_HOME"] = OPENCLAW_HOME,
                ["OPENCLAW_CONFIG_PATH"] = OPENCLAW_CONFIG,
            };
            StartProcess("cmd.exe", "/c openclaw gateway --port 18789", env);
            await Task.Delay(3000);
        }

        if (!bridgeUp)
        {
            var env = new Dictionary<string, string>
            {
                ["BRIDGE_TOKEN"] = BRIDGE_TOKEN,
                ["OPENCLAW_HOME"] = OPENCLAW_HOME,
            };
            StartProcess("node", BRIDGE_SCRIPT, env);
            await Task.Delay(2000);
        }

        if (!ngrokUp)
        {
            StartProcess(NGROK_EXE, $"http 18789 --traffic-policy-file=\"{NGROK_POLICY}\"");
            await Task.Delay(5000);
        }

        await RefreshStatus();
        btnStartAll.Enabled = true;
        btnStartAll.Text = "\u25b6 Start All";
    }

    async Task StopAll()
    {
        btnStopAll.Enabled = false;
        btnStopAll.Text = "Stopping...";

        KillByName("ngrok");
        KillNodeScript("bridge-server");
        RunCmd("cmd.exe", "/c openclaw gateway stop");
        KillByPort(18789);

        await Task.Delay(2000);
        await RefreshStatus();
        btnStopAll.Enabled = true;
        btnStopAll.Text = "\u25a0 Stop All";
    }

    static void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"{GATEWAY_URL}/#token={GATEWAY_TOKEN}",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    // ===================== Process Utilities =====================

    static void StartProcess(string exe, string args, Dictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        if (env != null)
            foreach (var kv in env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;

        try { Process.Start(psi); } catch { }
    }

    static void RunCmd(string exe, string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            });
            p?.WaitForExit(5000);
        }
        catch { }
    }

    static void TreeKill(int pid)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/T /F /PID {pid}",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            p?.WaitForExit(5000);
        }
        catch { }
    }

    static void KillByName(string name)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(name))
                TreeKill(p.Id);
        }
        catch { }
    }

    static void KillNodeScript(string scriptPattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Get-CimInstance Win32_Process -Filter \\\"Name='node.exe'\\\" | Where-Object {{$_.CommandLine -like '*{scriptPattern}*'}} | ForEach-Object {{$id=$_.ProcessId; Start-Process taskkill -ArgumentList '/T','/F','/PID',$id -NoNewWindow -Wait}}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch { }
    }

    static void KillByPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"netstat -ano | Select-String 'LISTENING' | Select-String ':{port}\\s' | ForEach-Object {{ ($_ -split '\\s+')[-1] }} | Sort-Object -Unique | ForEach-Object {{Start-Process taskkill -ArgumentList '/T','/F','/PID',$_ -NoNewWindow -Wait}}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            };
            Process.Start(psi)?.WaitForExit(5000);
        }
        catch { }
    }
}

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
    const string BRIDGE_SCRIPT = @"C:\OpenClawWorkspace\scripts\openclaw-bridge-server.js";
    const string BRIDGE_TOKEN = "{{BRIDGE_TOKEN}}";
    const string NGROK_EXE = @"{{NGROK_EXE_PATH}}";

    // --- UI Elements ---
    readonly Label lblTitle = new();
    readonly Label lblGateway = new();
    readonly Label lblBridge = new();
    readonly Label lblNgrok = new();
    readonly Label lblNgrokUrl = new();
    readonly Panel pnlGatewayDot = new();
    readonly Panel pnlBridgeDot = new();
    readonly Panel pnlNgrokDot = new();
    readonly Button btnStartAll = new();
    readonly Button btnStopAll = new();
    readonly Button btnRefresh = new();
    readonly Label lblLastCheck = new();
    readonly System.Windows.Forms.Timer timer = new();
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };

    bool gatewayUp, bridgeUp, ngrokUp;
    string ngrokUrl = "";

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
        Size = new Size(480, 380);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(24, 24, 32);
        Font = new Font("Segoe UI", 10f);

        // Title
        lblTitle.Text = "OpenClaw Control Panel";
        lblTitle.Font = new Font("Segoe UI Semibold", 16f);
        lblTitle.ForeColor = Color.FromArgb(200, 220, 255);
        lblTitle.Location = new Point(20, 16);
        lblTitle.AutoSize = true;
        Controls.Add(lblTitle);

        // Service rows
        int y = 70;
        AddServiceRow(pnlGatewayDot, lblGateway, "Gateway", ":18789", ref y);
        AddServiceRow(pnlBridgeDot, lblBridge, "Bridge", ":3847", ref y);
        AddServiceRow(pnlNgrokDot, lblNgrok, "ngrok", "tunnel", ref y);

        // ngrok URL
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
        y += 30;

        // Buttons
        btnStartAll.Text = "▶  Start All";
        btnStartAll.FlatStyle = FlatStyle.Flat;
        btnStartAll.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 100);
        btnStartAll.ForeColor = Color.FromArgb(60, 220, 120);
        btnStartAll.BackColor = Color.FromArgb(30, 50, 40);
        btnStartAll.Size = new Size(130, 40);
        btnStartAll.Location = new Point(20, y);
        btnStartAll.Cursor = Cursors.Hand;
        btnStartAll.Click += async (_, _) => await StartAll();
        Controls.Add(btnStartAll);

        btnStopAll.Text = "■  Stop All";
        btnStopAll.FlatStyle = FlatStyle.Flat;
        btnStopAll.FlatAppearance.BorderColor = Color.FromArgb(200, 80, 80);
        btnStopAll.ForeColor = Color.FromArgb(240, 100, 100);
        btnStopAll.BackColor = Color.FromArgb(50, 30, 30);
        btnStopAll.Size = new Size(130, 40);
        btnStopAll.Location = new Point(165, y);
        btnStopAll.Cursor = Cursors.Hand;
        btnStopAll.Click += async (_, _) => await StopAll();
        Controls.Add(btnStopAll);

        btnRefresh.Text = "↻  Refresh";
        btnRefresh.FlatStyle = FlatStyle.Flat;
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(100, 140, 200);
        btnRefresh.ForeColor = Color.FromArgb(140, 180, 240);
        btnRefresh.BackColor = Color.FromArgb(30, 35, 50);
        btnRefresh.Size = new Size(130, 40);
        btnRefresh.Location = new Point(310, y);
        btnRefresh.Cursor = Cursors.Hand;
        btnRefresh.Click += async (_, _) => await RefreshStatus();
        Controls.Add(btnRefresh);

        y += 55;
        lblLastCheck.Text = "";
        lblLastCheck.ForeColor = Color.FromArgb(90, 90, 110);
        lblLastCheck.Font = new Font("Segoe UI", 8f);
        lblLastCheck.Location = new Point(20, y);
        lblLastCheck.AutoSize = true;
        Controls.Add(lblLastCheck);
    }

    void AddServiceRow(Panel dot, Label lbl, string name, string detail, ref int y)
    {
        dot.Size = new Size(14, 14);
        dot.Location = new Point(24, y + 4);
        dot.BackColor = Color.FromArgb(80, 80, 80);
        MakeCircle(dot);
        Controls.Add(dot);

        lbl.Text = $"{name}  ({detail})  —  checking...";
        lbl.ForeColor = Color.FromArgb(180, 180, 195);
        lbl.Location = new Point(48, y);
        lbl.AutoSize = true;
        Controls.Add(lbl);

        y += 36;
    }

    static void MakeCircle(Panel p)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(0, 0, p.Width, p.Height);
        p.Region = new Region(path);
    }

    void SetDot(Panel dot, bool up)
    {
        dot.BackColor = up ? Color.FromArgb(60, 220, 120) : Color.FromArgb(200, 60, 60);
    }

    // --- Status Check ---
    async Task RefreshStatus()
    {
        gatewayUp = await CheckUrl(GATEWAY_URL);
        bridgeUp = await CheckUrl(BRIDGE_URL + "/health");
        (ngrokUp, ngrokUrl) = await CheckNgrok();

        if (InvokeRequired)
            Invoke(UpdateUI);
        else
            UpdateUI();
    }

    void UpdateUI()
    {
        SetDot(pnlGatewayDot, gatewayUp);
        SetDot(pnlBridgeDot, bridgeUp);
        SetDot(pnlNgrokDot, ngrokUp);

        lblGateway.Text = $"Gateway  (:18789)  —  {(gatewayUp ? "RUNNING" : "STOPPED")}";
        lblGateway.ForeColor = gatewayUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        lblBridge.Text = $"Bridge  (:3847)  —  {(bridgeUp ? "RUNNING" : "STOPPED")}";
        lblBridge.ForeColor = bridgeUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        lblNgrok.Text = $"ngrok  (tunnel)  —  {(ngrokUp ? "RUNNING" : "STOPPED")}";
        lblNgrok.ForeColor = ngrokUp ? Color.FromArgb(160, 240, 180) : Color.FromArgb(240, 140, 140);

        lblNgrokUrl.Text = ngrokUp ? $"  {ngrokUrl}  (click to copy)" : "";
        lblLastCheck.Text = $"Last check: {DateTime.Now:HH:mm:ss}   (auto-refresh 5s)";
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

    // --- Start / Stop ---
    async Task StartAll()
    {
        btnStartAll.Enabled = false;
        btnStartAll.Text = "Starting...";

        if (!gatewayUp) StartProcess("cmd.exe", "/c openclaw gateway --port 18789");
        await Task.Delay(3000);

        if (!bridgeUp)
        {
            var env = new Dictionary<string, string> { ["BRIDGE_TOKEN"] = BRIDGE_TOKEN };
            StartProcess("node", BRIDGE_SCRIPT, env);
        }
        await Task.Delay(2000);

        if (!ngrokUp) StartProcess(NGROK_EXE, "http 18789");
        await Task.Delay(5000);

        await RefreshStatus();
        btnStartAll.Enabled = true;
        btnStartAll.Text = "▶  Start All";
    }

    async Task StopAll()
    {
        btnStopAll.Enabled = false;
        btnStopAll.Text = "Stopping...";

        KillByName("ngrok");
        KillNodeScript("bridge-server");
        KillByPort(18789);

        await Task.Delay(2000);
        await RefreshStatus();
        btnStopAll.Enabled = true;
        btnStopAll.Text = "■  Stop All";
    }

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

    // taskkill /T /F kills entire process tree (parent + all children)
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
            // Get-Process.CommandLine is null in Windows PowerShell 5.1
            // Use Get-CimInstance Win32_Process to get the actual command line, then tree-kill
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
            // Find PID listening on port, then tree-kill
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

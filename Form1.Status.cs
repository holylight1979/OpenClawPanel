using System.Diagnostics;
using System.Text.Json;

namespace OpenClawPanel;

partial class Form1
{
    // ===================== Status Check =====================

    async Task RefreshStatus()
    {
        gatewayUp = await CheckUrl(GATEWAY_URL + "/health");
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
        SetDot(pnlNgrokDot, ngrokUp);

        // Service labels
        lblGateway.Text = $"Gateway  (:{GATEWAY_PORT})  \u2014  {(gatewayUp ? "RUNNING" : "STOPPED")}";
        lblGateway.ForeColor = gatewayUp ? FgRunning : FgStopped;

        lblNgrok.Text = $"ngrok  (tunnel)  \u2014  {(ngrokUp ? "RUNNING" : "STOPPED")}";
        lblNgrok.ForeColor = ngrokUp ? FgRunning : FgStopped;

        // Per-service button enable/disable
        btnGwStart.Enabled = !gatewayUp;
        btnGwStop.Enabled = gatewayUp;
        btnNgStart.Enabled = !ngrokUp;
        btnNgStop.Enabled = ngrokUp;

        // ngrok URL on Control tab
        lblNgrokUrl.Text = ngrokUp ? $"  {ngrokUrl}  (click to copy)" : "";

        // Timestamp
        lblLastCheck.Text = $"Last check: {DateTime.Now:HH:mm:ss}   (auto-refresh 5s)";

        // Port list
        UpdatePortsList();

        // Connection tab
        UpdateConnectionTab();
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
                    : Color.FromArgb(200, 160, 100),
                Location = new Point(62, y + 4),
                Size = new Size(200, 20),
            };
            var lblPid = new Label
            {
                Text = entry.Pid.ToString(),
                Font = new Font("Consolas", 8.5f),
                ForeColor = Color.FromArgb(130, 140, 160),
                Location = new Point(268, y + 4),
                Size = new Size(60, 20),
            };
            var lblProc = new Label
            {
                Text = entry.ProcessName,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 130, 150),
                Location = new Point(334, y + 4),
                Size = new Size(160, 20),
            };

            var btnKill = new Button
            {
                Text = "Kill",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(240, 120, 120),
                BackColor = Color.FromArgb(45, 28, 28),
                Size = new Size(48, 24),
                Location = new Point(730, y + 1),
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
}

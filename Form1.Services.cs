using System.Diagnostics;

namespace OpenClawPanel;

partial class Form1
{
    // ===================== Individual Service Control =====================

    async Task StartGateway()
    {
        btnGwStart.Enabled = false;
        await TryStartGateway();
        await RefreshStatus();
        btnGwStart.Enabled = true;
    }

    async Task<bool> TryStartGateway()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{OPENCLAW_CLI}\" gateway --port {GATEWAY_PORT}",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        // Load OpenClaw .env (e.g. DISCORD_BOT_TOKEN) and merge with panel env
        var openClawEnv = LoadEnvFile(OPENCLAW_DOTENV);
        foreach (var kv in openClawEnv)
            psi.EnvironmentVariables[kv.Key] = kv.Value;
        foreach (var kv in envVars)
            psi.EnvironmentVariables[kv.Key] = kv.Value;
        psi.EnvironmentVariables["OPENCLAW_HOME"] = OPENCLAW_HOME;
        psi.EnvironmentVariables["OPENCLAW_CONFIG_PATH"] = OPENCLAW_CONFIG;

        var stderr = new System.Text.StringBuilder();
        Process? proc = null;
        try
        {
            proc = Process.Start(psi);
            if (proc != null)
            {
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                proc.OutputDataReceived += (_, _) => { };
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch Gateway process.\n{ex.Message}",
                "Gateway Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (proc == null)
        {
            MessageBox.Show("Failed to launch Gateway process.",
                "Gateway Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        // Poll health for up to 15 seconds (cold start takes ~11s)
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(1000);
            if (await CheckUrl(GATEWAY_URL + "/health"))
            {
                try { proc.CancelErrorRead(); proc.CancelOutputRead(); } catch { }
                return true;
            }
            if (proc.HasExited) break;
        }

        var errText = stderr.ToString().Trim();
        MessageBox.Show(
            string.IsNullOrEmpty(errText)
                ? "Gateway failed to start — health check timed out after 15s."
                : errText,
            "Gateway Start Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    async Task StopGateway()
    {
        btnGwStop.Enabled = false;
        RunCmd("node", $"\"{OPENCLAW_CLI}\" gateway stop");
        KillByPort(GATEWAY_PORT);
        await Task.Delay(2000);
        await RefreshStatus();
        btnGwStop.Enabled = true;
    }

    async Task StartNgrok()
    {
        btnNgStart.Enabled = false;
        StartProcess(NGROK_EXE, $"http {GATEWAY_PORT} --traffic-policy-file=\"{NGROK_POLICY}\"");
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
            await TryStartGateway();

        if (!ngrokUp)
        {
            StartProcess(NGROK_EXE, $"http {GATEWAY_PORT} --traffic-policy-file=\"{NGROK_POLICY}\"");
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
        RunCmd("node", $"\"{OPENCLAW_CLI}\" gateway stop");
        KillByPort(GATEWAY_PORT);

        await Task.Delay(2000);
        await RefreshStatus();
        btnStopAll.Enabled = true;
        btnStopAll.Text = "\u25a0 Stop All";
    }

    void OpenWebView()
    {
        var gw = activePorts.FirstOrDefault(p => p.Service == "Gateway");
        var port = gw?.Port ?? GATEWAY_PORT;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"http://127.0.0.1:{port}/#token={GATEWAY_TOKEN}",
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

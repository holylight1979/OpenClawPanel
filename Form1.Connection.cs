using System.Diagnostics;

namespace OpenClawPanel;

partial class Form1
{
    void UpdateConnectionTab()
    {
        // Update ngrok URL
        txtNgrokUrl.Text = ngrokUp ? ngrokUrl : "(ngrok not running)";

        // Update webhook URLs
        if (ngrokUp && !string.IsNullOrEmpty(ngrokUrl))
        {
            txtLineWebhook.Text = $"{ngrokUrl}/line/webhook";
        }
        else
        {
            txtLineWebhook.Text = "(ngrok not running)";
        }

        // Update Gateway status
        SetDot(pnlGwStatusDot, gatewayUp);
        if (gatewayUp)
        {
            var gw = activePorts.FirstOrDefault(p => p.Service == "Gateway");
            if (gw != null)
            {
                var uptime = GetProcessUptime(gw.Pid);
                lblGwStatusInfo.Text = $"RUNNING  |  Port: {gw.Port}  |  PID: {gw.Pid}  |  Uptime: {uptime}";
                lblGwStatusInfo.ForeColor = FgRunning;
            }
            else
            {
                lblGwStatusInfo.Text = $"RUNNING  |  Port: {GATEWAY_PORT}";
                lblGwStatusInfo.ForeColor = FgRunning;
            }
        }
        else
        {
            lblGwStatusInfo.Text = "STOPPED";
            lblGwStatusInfo.ForeColor = FgStopped;
        }

        // Check for ngrok URL change
        DetectNgrokUrlChange();
    }

    void DetectNgrokUrlChange()
    {
        if (string.IsNullOrEmpty(ngrokUrl))
            return;

        if (string.IsNullOrEmpty(lastKnownNgrokUrl))
        {
            // First time seeing a URL, just record it
            lastKnownNgrokUrl = ngrokUrl;
            return;
        }

        if (ngrokUrl != lastKnownNgrokUrl)
        {
            // URL has changed!
            lastKnownNgrokUrl = ngrokUrl;
            alertDismissed = false;
            pnlAlert.Visible = true;

            // Switch to Connection tab to draw attention
            tabControl.SelectedTab = tabConnection;
        }

        // Keep showing alert until dismissed
        if (!alertDismissed && pnlAlert.Visible)
            pnlAlert.Visible = true;
    }

    static string GetProcessUptime(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            var uptime = DateTime.Now - proc.StartTime;
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
            if (uptime.TotalHours >= 1)
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }
        catch
        {
            return "N/A";
        }
    }
}

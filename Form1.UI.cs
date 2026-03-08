using System.Diagnostics;

namespace OpenClawPanel;

partial class Form1
{
    // --- Theme Colors ---
    static readonly Color BgDark = Color.FromArgb(24, 24, 32);
    static readonly Color BgPanel = Color.FromArgb(30, 30, 40);
    static readonly Color BgInput = Color.FromArgb(20, 20, 28);
    static readonly Color FgTitle = Color.FromArgb(200, 220, 255);
    static readonly Color FgLabel = Color.FromArgb(180, 180, 195);
    static readonly Color FgDim = Color.FromArgb(100, 110, 140);
    static readonly Color FgRunning = Color.FromArgb(160, 240, 180);
    static readonly Color FgStopped = Color.FromArgb(240, 140, 140);
    static readonly Color GreenDot = Color.FromArgb(60, 220, 120);
    static readonly Color RedDot = Color.FromArgb(200, 60, 60);
    static readonly Color GrayDot = Color.FromArgb(80, 80, 80);
    static readonly Color AlertBg = Color.FromArgb(60, 50, 20);
    static readonly Color AlertFg = Color.FromArgb(255, 200, 80);

    void SetupUI()
    {
        Text = "OpenClaw Panel";
        Size = new Size(880, 720);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = BgDark;
        Font = new Font("Segoe UI", 10f);

        // --- Title ---
        lblTitle.Text = "OpenClaw Control Panel";
        lblTitle.Font = new Font("Segoe UI Semibold", 16f);
        lblTitle.ForeColor = FgTitle;
        lblTitle.Location = new Point(20, 12);
        lblTitle.AutoSize = true;
        Controls.Add(lblTitle);

        // --- TabControl ---
        tabControl.Location = new Point(10, 48);
        tabControl.Size = new Size(846, 624);
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(120, 32);
        tabControl.DrawItem += TabControl_DrawItem;
        tabControl.TabPages.AddRange([tabControl_, tabLogs, tabConnection]);
        Controls.Add(tabControl);

        // Style tab pages
        foreach (TabPage tp in tabControl.TabPages)
        {
            tp.BackColor = BgDark;
            tp.ForeColor = FgLabel;
        }

        SetupControlTab();
        SetupLogsTab();
        SetupConnectionTab();
    }

    void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var page = tabControl.TabPages[e.Index];
        var isSelected = e.Index == tabControl.SelectedIndex;
        var bg = isSelected ? Color.FromArgb(40, 40, 55) : Color.FromArgb(28, 28, 38);
        var fg = isSelected ? FgTitle : FgDim;

        using var bgBrush = new SolidBrush(bg);
        using var fgBrush = new SolidBrush(fg);
        e.Graphics!.FillRectangle(bgBrush, e.Bounds);

        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        e.Graphics.DrawString(page.Text, new Font("Segoe UI", 10f),
            fgBrush, e.Bounds, sf);
    }

    // ===================== Control Tab =====================

    void SetupControlTab()
    {
        int y = 16;

        // --- Service Rows ---
        SetupServiceRow(tabControl_, pnlGatewayDot, lblGateway, "Gateway", $":{GATEWAY_PORT}",
            btnGwStart, btnGwStop, ref y);
        btnGwStart.Click += async (_, _) => await StartGateway();
        btnGwStop.Click += async (_, _) => await StopGateway();

        SetupServiceRow(tabControl_, pnlNgrokDot, lblNgrok, "ngrok", "tunnel",
            btnNgStart, btnNgStop, ref y);
        btnNgStart.Click += async (_, _) => await StartNgrok();
        btnNgStop.Click += async (_, _) => await StopNgrok();

        // ngrok URL (click to copy)
        lblNgrokUrl.Text = "";
        lblNgrokUrl.ForeColor = Color.FromArgb(120, 160, 200);
        lblNgrokUrl.Font = new Font("Consolas", 8.5f);
        lblNgrokUrl.Location = new Point(44, y);
        lblNgrokUrl.Size = new Size(500, 20);
        lblNgrokUrl.Cursor = Cursors.Hand;
        lblNgrokUrl.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(ngrokUrl))
                Clipboard.SetText(ngrokUrl);
        };
        tabControl_.Controls.Add(lblNgrokUrl);
        y += 28;

        // --- Global Buttons ---
        MakeButton(tabControl_, btnStartAll, "\u25b6 Start All", 10, y, 100, 36,
            GreenDot, Color.FromArgb(30, 50, 40), Color.FromArgb(60, 180, 100));
        btnStartAll.Click += async (_, _) => await StartAll();

        MakeButton(tabControl_, btnStopAll, "\u25a0 Stop All", 114, y, 100, 36,
            Color.FromArgb(240, 100, 100), Color.FromArgb(50, 30, 30), Color.FromArgb(200, 80, 80));
        btnStopAll.Click += async (_, _) => await StopAll();

        MakeButton(tabControl_, btnWebView, "\ud83c\udf10 Dashboard", 218, y, 110, 36,
            Color.FromArgb(160, 220, 255), Color.FromArgb(25, 35, 48), Color.FromArgb(80, 140, 190));
        btnWebView.Click += (_, _) => OpenWebView();

        MakeButton(tabControl_, btnRefresh, "\u21bb Refresh", 332, y, 100, 36,
            Color.FromArgb(140, 180, 240), Color.FromArgb(30, 35, 50), Color.FromArgb(100, 140, 200));
        btnRefresh.Click += async (_, _) => await RefreshStatus();
        y += 50;

        // --- Ports Section ---
        lblPortsHeader.Text = "\u2500\u2500\u2500 Active Ports \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500";
        lblPortsHeader.ForeColor = FgDim;
        lblPortsHeader.Font = new Font("Segoe UI", 9f);
        lblPortsHeader.Location = new Point(14, y);
        lblPortsHeader.AutoSize = true;
        tabControl_.Controls.Add(lblPortsHeader);

        MakeButton(tabControl_, btnScan, "\ud83d\udd0d Scan", 740, y - 4, 76, 26,
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
        tabControl_.Controls.Add(lblPortColHdr);
        y += 20;

        // Scrollable port list panel
        pnlPorts.Location = new Point(14, y);
        pnlPorts.Size = new Size(800, 300);
        pnlPorts.AutoScroll = true;
        pnlPorts.BackColor = BgInput;
        tabControl_.Controls.Add(pnlPorts);
        y += 308;

        // Last check
        lblLastCheck.Text = "";
        lblLastCheck.ForeColor = Color.FromArgb(90, 90, 110);
        lblLastCheck.Font = new Font("Segoe UI", 8f);
        lblLastCheck.Location = new Point(20, y);
        lblLastCheck.AutoSize = true;
        tabControl_.Controls.Add(lblLastCheck);
    }

    // ===================== Logs Tab =====================

    void SetupLogsTab()
    {
        // Toolbar
        int y = 8;

        MakeButton(tabLogs, btnLogClear, "Clear", 10, y, 70, 28,
            Color.FromArgb(200, 180, 160), Color.FromArgb(35, 32, 40), Color.FromArgb(120, 100, 90));
        btnLogClear.Font = new Font("Segoe UI", 9f);
        btnLogClear.Click += (_, _) =>
        {
            rtbLog.Clear();
            logLineCount = 0;
        };

        chkAutoScroll.Text = "Auto-scroll";
        chkAutoScroll.Checked = true;
        chkAutoScroll.ForeColor = FgLabel;
        chkAutoScroll.Location = new Point(90, y + 3);
        chkAutoScroll.AutoSize = true;
        chkAutoScroll.Font = new Font("Segoe UI", 9f);
        tabLogs.Controls.Add(chkAutoScroll);

        lblLogFile.Text = "";
        lblLogFile.ForeColor = FgDim;
        lblLogFile.Font = new Font("Consolas", 8f);
        lblLogFile.Location = new Point(220, y + 6);
        lblLogFile.AutoSize = true;
        tabLogs.Controls.Add(lblLogFile);

        y += 36;

        // Log display
        rtbLog.Location = new Point(10, y);
        rtbLog.Size = new Size(820, 544);
        rtbLog.Font = new Font("Consolas", 9f);
        rtbLog.BackColor = Color.FromArgb(16, 16, 24);
        rtbLog.ForeColor = Color.FromArgb(180, 190, 200);
        rtbLog.ReadOnly = true;
        rtbLog.WordWrap = false;
        rtbLog.ScrollBars = RichTextBoxScrollBars.Both;
        rtbLog.BorderStyle = BorderStyle.None;
        tabLogs.Controls.Add(rtbLog);
    }

    // ===================== Connection Tab =====================

    void SetupConnectionTab()
    {
        int y = 10;

        // --- Alert Banner (hidden by default) ---
        pnlAlert.Location = new Point(10, y);
        pnlAlert.Size = new Size(820, 50);
        pnlAlert.BackColor = AlertBg;
        pnlAlert.Visible = false;
        tabConnection.Controls.Add(pnlAlert);

        lblAlertText.Text = "\u26a0 ngrok URL \u5df2\u8b8a\u66f4\uff01\u8acb\u66f4\u65b0 LINE Developers Webhook";
        lblAlertText.Font = new Font("Segoe UI Semibold", 10f);
        lblAlertText.ForeColor = AlertFg;
        lblAlertText.Location = new Point(12, 14);
        lblAlertText.AutoSize = true;
        pnlAlert.Controls.Add(lblAlertText);

        MakeButton(pnlAlert, btnOpenLineConsole, "LINE Console", 480, 10, 120, 30,
            Color.FromArgb(120, 220, 160), Color.FromArgb(30, 45, 35), Color.FromArgb(80, 160, 100));
        btnOpenLineConsole.Font = new Font("Segoe UI", 9f);
        btnOpenLineConsole.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://developers.line.biz/console/",
                    UseShellExecute = true,
                });
            }
            catch { }
        };

        MakeButton(pnlAlert, btnDismissAlert, "\u2715", 760, 10, 40, 30,
            Color.FromArgb(200, 180, 140), Color.FromArgb(50, 45, 30), Color.FromArgb(140, 120, 80));
        btnDismissAlert.Font = new Font("Segoe UI", 10f);
        btnDismissAlert.Click += (_, _) =>
        {
            pnlAlert.Visible = false;
            alertDismissed = true;
        };

        y += 60;

        // --- Section: ngrok Public URL ---
        AddSectionLabel(tabConnection, "\u2500 ngrok Public URL", ref y);

        txtNgrokUrl.Location = new Point(20, y);
        txtNgrokUrl.Size = new Size(660, 28);
        txtNgrokUrl.Font = new Font("Consolas", 10f);
        txtNgrokUrl.BackColor = BgInput;
        txtNgrokUrl.ForeColor = Color.FromArgb(140, 200, 240);
        txtNgrokUrl.BorderStyle = BorderStyle.FixedSingle;
        txtNgrokUrl.ReadOnly = true;
        tabConnection.Controls.Add(txtNgrokUrl);

        MakeCopyButton(tabConnection, btnCopyNgrokUrl, 690, y, () => txtNgrokUrl.Text);
        y += 44;

        // --- Section: Webhook URLs ---
        AddSectionLabel(tabConnection, "\u2500 Channel Webhook URLs", ref y);

        lblLineLabel.Text = "LINE:";
        lblLineLabel.Font = new Font("Segoe UI Semibold", 9.5f);
        lblLineLabel.ForeColor = Color.FromArgb(100, 200, 120);
        lblLineLabel.Location = new Point(20, y + 3);
        lblLineLabel.AutoSize = true;
        tabConnection.Controls.Add(lblLineLabel);

        txtLineWebhook.Location = new Point(80, y);
        txtLineWebhook.Size = new Size(600, 28);
        txtLineWebhook.Font = new Font("Consolas", 9.5f);
        txtLineWebhook.BackColor = BgInput;
        txtLineWebhook.ForeColor = Color.FromArgb(140, 200, 240);
        txtLineWebhook.BorderStyle = BorderStyle.FixedSingle;
        txtLineWebhook.ReadOnly = true;
        tabConnection.Controls.Add(txtLineWebhook);

        MakeCopyButton(tabConnection, btnCopyLineWebhook, 690, y, () => txtLineWebhook.Text);
        y += 44;

        // --- Section: Gateway Token ---
        AddSectionLabel(tabConnection, "\u2500 Gateway Token", ref y);

        txtGatewayToken.Location = new Point(20, y);
        txtGatewayToken.Size = new Size(600, 28);
        txtGatewayToken.Font = new Font("Consolas", 10f);
        txtGatewayToken.BackColor = BgInput;
        txtGatewayToken.ForeColor = Color.FromArgb(200, 180, 140);
        txtGatewayToken.BorderStyle = BorderStyle.FixedSingle;
        txtGatewayToken.ReadOnly = true;
        txtGatewayToken.UseSystemPasswordChar = true;
        txtGatewayToken.Text = GATEWAY_TOKEN;
        tabConnection.Controls.Add(txtGatewayToken);

        MakeButton(tabConnection, btnToggleToken, "\ud83d\udc41", 630, y, 40, 28,
            Color.FromArgb(180, 180, 200), Color.FromArgb(35, 35, 48), Color.FromArgb(100, 100, 130));
        btnToggleToken.Font = new Font("Segoe UI", 10f);
        btnToggleToken.Click += (_, _) =>
        {
            txtGatewayToken.UseSystemPasswordChar = !txtGatewayToken.UseSystemPasswordChar;
        };

        MakeCopyButton(tabConnection, btnCopyToken, 690, y, () => GATEWAY_TOKEN);
        y += 50;

        // --- Section: Gateway Status ---
        AddSectionLabel(tabConnection, "\u2500 Gateway Status", ref y);

        pnlGwStatusDot.Size = new Size(14, 14);
        pnlGwStatusDot.Location = new Point(20, y + 3);
        pnlGwStatusDot.BackColor = GrayDot;
        MakeCircle(pnlGwStatusDot);
        tabConnection.Controls.Add(pnlGwStatusDot);

        lblGwStatusInfo.Text = "Checking...";
        lblGwStatusInfo.Font = new Font("Consolas", 9.5f);
        lblGwStatusInfo.ForeColor = FgLabel;
        lblGwStatusInfo.Location = new Point(44, y);
        lblGwStatusInfo.AutoSize = true;
        tabConnection.Controls.Add(lblGwStatusInfo);
    }

    // ===================== UI Helpers =====================

    void AddSectionLabel(Control parent, string text, ref int y)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = FgDim,
            Location = new Point(12, y),
            AutoSize = true,
        };
        parent.Controls.Add(lbl);
        y += 26;
    }

    void MakeCopyButton(Control parent, Button btn, int x, int y, Func<string> getValue)
    {
        MakeButton(parent, btn, "Copy", x, y, 60, 28,
            Color.FromArgb(140, 180, 220), Color.FromArgb(30, 35, 48), Color.FromArgb(80, 110, 160));
        btn.Font = new Font("Segoe UI", 8.5f);
        btn.Click += async (_, _) =>
        {
            var val = getValue();
            if (!string.IsNullOrEmpty(val))
            {
                Clipboard.SetText(val);
                var orig = btn.Text;
                btn.Text = "Copied!";
                btn.ForeColor = GreenDot;
                await Task.Delay(1500);
                btn.Text = orig;
                btn.ForeColor = Color.FromArgb(140, 180, 220);
            }
        };
    }

    void SetupServiceRow(Control parent, Panel dot, Label lbl, string name, string detail,
        Button btnStart, Button btnStop, ref int y)
    {
        // Status dot
        dot.Size = new Size(14, 14);
        dot.Location = new Point(24, y + 5);
        dot.BackColor = GrayDot;
        MakeCircle(dot);
        parent.Controls.Add(dot);

        // Service label
        lbl.Text = $"{name}  ({detail})  \u2014  checking...";
        lbl.ForeColor = FgLabel;
        lbl.Location = new Point(48, y + 1);
        lbl.AutoSize = true;
        parent.Controls.Add(lbl);

        // Individual Start button
        btnStart.Text = "\u25b6";
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.ForeColor = Color.FromArgb(60, 200, 100);
        btnStart.BackColor = Color.FromArgb(28, 40, 32);
        btnStart.FlatAppearance.BorderColor = Color.FromArgb(50, 140, 80);
        btnStart.Size = new Size(36, 26);
        btnStart.Location = new Point(700, y);
        btnStart.Cursor = Cursors.Hand;
        btnStart.Font = new Font("Segoe UI", 9f);
        parent.Controls.Add(btnStart);

        // Individual Stop button
        btnStop.Text = "\u25a0";
        btnStop.FlatStyle = FlatStyle.Flat;
        btnStop.ForeColor = Color.FromArgb(220, 90, 90);
        btnStop.BackColor = Color.FromArgb(40, 28, 28);
        btnStop.FlatAppearance.BorderColor = Color.FromArgb(160, 70, 70);
        btnStop.Size = new Size(36, 26);
        btnStop.Location = new Point(742, y);
        btnStop.Cursor = Cursors.Hand;
        btnStop.Font = new Font("Segoe UI", 9f);
        parent.Controls.Add(btnStop);

        y += 34;
    }

    void MakeButton(Control parent, Button btn, string text, int x, int y, int w, int h,
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
        parent.Controls.Add(btn);
    }

    static void MakeCircle(Panel p)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(0, 0, p.Width, p.Height);
        p.Region = new Region(path);
    }

    void SetDot(Panel dot, bool up) =>
        dot.BackColor = up ? GreenDot : RedDot;
}

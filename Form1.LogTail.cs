using System.Text;
using System.Text.Json;

namespace OpenClawPanel;

partial class Form1
{
    static string GetTodayLogPath() =>
        Path.Combine(Path.GetTempPath(), "openclaw", $"openclaw-{DateTime.Now:yyyy-MM-dd}.log");

    void InitLogTail()
    {
        var logPath = GetTodayLogPath();
        currentLogDate = DateTime.Now.ToString("yyyy-MM-dd");
        lblLogFile.Text = logPath;

        // Initial read of existing log (last 200 lines)
        LoadExistingLog(logPath);

        // Setup FileSystemWatcher
        SetupLogWatcher();
    }

    void LoadExistingLog(string logPath)
    {
        if (!File.Exists(logPath)) return;

        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var allLines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                allLines.Add(line);

            lastLogReadPosition = fs.Position;

            // Take last 200 lines for initial display
            var startIdx = Math.Max(0, allLines.Count - 200);
            var sb = new StringBuilder();
            for (int i = startIdx; i < allLines.Count; i++)
            {
                var formatted = FormatLogLine(allLines[i]);
                if (formatted != null)
                    sb.AppendLine(formatted);
            }

            if (sb.Length > 0)
            {
                rtbLog.Text = sb.ToString();
                logLineCount = allLines.Count - startIdx;
                ScrollLogToEnd();
            }
        }
        catch { }
    }

    void SetupLogWatcher()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "openclaw");
        if (!Directory.Exists(logDir))
        {
            try { Directory.CreateDirectory(logDir); } catch { return; }
        }

        logWatcher?.Dispose();
        logWatcher = new FileSystemWatcher(logDir, "openclaw-*.log")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        logWatcher.Changed += (_, _) => PollLogFile();
    }

    void PollLogFile()
    {
        // Check for date rollover
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (today != currentLogDate)
        {
            currentLogDate = today;
            lastLogReadPosition = 0;
            if (InvokeRequired)
                BeginInvoke(() =>
                {
                    lblLogFile.Text = GetTodayLogPath();
                    rtbLog.Clear();
                    logLineCount = 0;
                });
            else
            {
                lblLogFile.Text = GetTodayLogPath();
                rtbLog.Clear();
                logLineCount = 0;
            }
        }

        var logPath = GetTodayLogPath();
        if (!File.Exists(logPath)) return;

        try
        {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= lastLogReadPosition) return;

            fs.Seek(lastLogReadPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            var newLines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var formatted = FormatLogLine(line);
                if (formatted != null)
                    newLines.Add(formatted);
            }

            lastLogReadPosition = fs.Position;

            if (newLines.Count > 0)
            {
                if (InvokeRequired)
                    BeginInvoke(() => AppendLogLines(newLines));
                else
                    AppendLogLines(newLines);
            }
        }
        catch { }
    }

    void AppendLogLines(List<string> lines)
    {
        rtbLog.SuspendLayout();

        foreach (var line in lines)
        {
            rtbLog.AppendText(line + Environment.NewLine);
            logLineCount++;
        }

        // Cap at 5000 lines, remove oldest 1000
        if (logLineCount > 5000)
        {
            TrimLogDisplay();
        }

        rtbLog.ResumeLayout();

        if (chkAutoScroll.Checked)
            ScrollLogToEnd();
    }

    void TrimLogDisplay()
    {
        // Find the position of the 1000th newline
        var text = rtbLog.Text;
        int pos = 0;
        for (int i = 0; i < 1000 && pos < text.Length; i++)
        {
            pos = text.IndexOf('\n', pos);
            if (pos < 0) break;
            pos++;
        }

        if (pos > 0 && pos < text.Length)
        {
            rtbLog.Select(0, pos);
            rtbLog.SelectedText = "";
            logLineCount -= 1000;
        }
    }

    void ScrollLogToEnd()
    {
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.ScrollToCaret();
    }

    string? FormatLogLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawLine);
            var root = doc.RootElement;

            // Extract time
            var time = "";
            if (root.TryGetProperty("time", out var timeProp))
            {
                var timeStr = timeProp.GetString() ?? "";
                // Parse "2026-03-08T19:23:46.630+08:00" → "19:23:46"
                if (DateTimeOffset.TryParse(timeStr, out var dto))
                    time = dto.ToString("HH:mm:ss");
            }

            // Extract subsystem from "0" field
            var subsystem = "";
            if (root.TryGetProperty("0", out var field0))
            {
                var raw0 = StripAnsi(field0.GetString() ?? "");
                // Try to parse as JSON to extract subsystem name
                try
                {
                    using var subDoc = JsonDocument.Parse(raw0);
                    if (subDoc.RootElement.TryGetProperty("subsystem", out var subProp))
                        subsystem = subProp.GetString() ?? "";
                }
                catch
                {
                    // Not JSON, use raw value (truncated)
                    subsystem = raw0.Length > 40 ? raw0[..40] : raw0;
                }
            }

            // Extract message from "1" field (or fall back to "0" if no "1")
            var message = "";
            if (root.TryGetProperty("1", out var field1))
            {
                message = StripAnsi(field1.GetString() ?? "");
            }
            else
            {
                // No "1" field, the message is in "0" (already extracted as subsystem)
                var raw0 = StripAnsi(root.GetProperty("0").GetString() ?? "");
                // If we already parsed subsystem from it, just show the raw message
                if (!string.IsNullOrEmpty(subsystem))
                    message = raw0;
                else
                {
                    subsystem = "log";
                    message = raw0;
                }
            }

            // Format: "19:23:46 [gateway/ws] → event tick seq=219"
            var sub = string.IsNullOrEmpty(subsystem) ? "" : $" [{subsystem}]";
            return $"{time}{sub} {message}";
        }
        catch
        {
            // Not valid JSON, display raw (stripped of ANSI)
            return StripAnsi(rawLine);
        }
    }
}

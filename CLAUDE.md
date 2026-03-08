# OpenClawPanel — 專案導讀 (Claude Code)

> WinForms 桌面控制面板，管理 OpenClaw Gateway + ngrok 服務的啟停與監控。

## 風險分級（專案特定）

> 通用分級框架見全域 `~/.claude/CLAUDE.md`。

| 風險等級 | 本專案操作類型 | 驗證要求 |
|---------|--------------|---------|
| **高** | 修改服務啟停邏輯（Form1.Services.cs）、port 掃描/kill 邏輯 | 必須先讀取 _AIDocs + 原始碼 |
| **極高** | 修改 .env 設定值、Gateway token 相關邏輯 | 必須向使用者確認後才執行 |

## 技術約束

- .NET 9.0-windows + WinForms，無額外 NuGet 套件
- 單一 Form1 partial class 拆為 7 個檔案，依職責分離
- 設定來自 .env（路徑/port）+ openclaw.json（Gateway token）
- Gateway 啟動方式：`node openclaw.mjs gateway --port {port}`
- Log 路徑：`%TEMP%/openclaw/openclaw-YYYY-MM-DD.log`（JSON 格式）
- UI 為純程式碼建立（非 Designer），深色主題

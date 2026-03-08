# OpenClawPanel — 專案結構

## 技術棧

- **框架**: .NET 9.0 + Windows Forms
- **語言**: C# (implicit usings, nullable enabled)
- **外部依賴**: 無 NuGet 套件（純 .NET SDK）
- **設定**: `.env` 檔案（複製到輸出目錄）+ `openclaw.json`（Gateway token）

## 檔案結構

```
OpenClawPanel/
├── .env                    # 環境設定（路徑、port、URL）
├── .gitignore
├── OpenClawPanel.csproj    # .NET 9.0-windows WinExe
├── OpenClawPanel.slnx      # Solution file
├── Program.cs              # 進入點 → Application.Run(new Form1())
│
├── Form1.cs                # 主類別：.env 載入、設定解析、UI 元件宣告、建構子
├── Form1.Designer.cs       # Designer（最小化，主要 UI 在 Form1.UI.cs）
├── Form1.UI.cs             # 佈局建立：三分頁 TabControl、主題色彩、按鈕/標籤設定
├── Form1.Services.cs       # 服務控制：Gateway/ngrok 啟停、Start All/Stop All、process 工具
├── Form1.Status.cs         # 狀態檢查：health check、ngrok API、port 掃描、UI 更新
├── Form1.Connection.cs     # Connection 分頁：ngrok URL 顯示、webhook URL、Gateway 狀態、URL 變更偵測
├── Form1.LogTail.cs        # Log 分頁：FileSystemWatcher + 輪詢、JSON log 格式化、5000 行上限
│
├── _AIDocs/                # AI 知識庫
└── memory/                 # 決策記憶
```

## 架構概覽

單一 `Form1` partial class 拆分為 7 個檔案：

| 檔案 | 職責 |
|------|------|
| Form1.cs | 設定載入（.env → static fields）、UI 元件宣告、Timer 初始化 |
| Form1.UI.cs | 深色主題色彩定義、三分頁 UI 佈局建立（Control / Logs / Connection） |
| Form1.Services.cs | Gateway 啟動（node openclaw.mjs gateway）、ngrok 啟動、Stop/Kill 工具 |
| Form1.Status.cs | 5 秒定時 health check、ngrok tunnel API 查詢、netstat port 掃描 |
| Form1.Connection.cs | Connection tab 更新邏輯、ngrok URL 變更警示、Gateway uptime 計算 |
| Form1.LogTail.cs | 讀取 %TEMP%/openclaw/openclaw-YYYY-MM-DD.log、JSON 解析格式化、自動捲動 |

## 關鍵設定值（來自 .env）

| 變數 | 用途 |
|------|------|
| OPENCLAW_HOME | OpenClaw 安裝目錄 |
| OPENCLAW_CLI | CLI 入口 (openclaw.mjs) |
| OPENCLAW_CONFIG | openclaw.json 路徑（含 Gateway token） |
| NGROK_EXE | ngrok 執行檔路徑 |
| NGROK_POLICY | ngrok traffic policy 檔案 |
| GATEWAY_PORT | Gateway 主 port（預設 18789） |
| GATEWAY_URL | Gateway base URL |
| NGROK_API | ngrok 本地 API |

## Port 掃描範圍

- Gateway 範圍: `GATEWAY_PORT ± ~10`
- ngrok 範圍: `4030–4050`
- 已知 port: Gateway (18789), BrowserCtl (+2), Internal (+3), ngrok API (4040)

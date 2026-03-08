# OpenClawPanel — 變更記錄

| 日期 | 變更 | 影響範圍 |
|------|------|---------|
| 2026-03-08 | 移除硬編碼路徑：所有設定改由 .env 讀取，Gateway 啟動時載入 OPENCLAW_DOTENV | Form1.cs, Form1.Services.cs, .env |
| 2026-03-08 | **重大重構：TabControl 三分頁架構** — 單一 Form1.cs (760行) 拆分為 6 個 partial class；視窗 580x550 → 880x720；新增 Control / Logs / Connection 三個 tab；OwnerDraw 暗色主題 TabControl | Form1.cs, Form1.UI.cs, Form1.Status.cs, Form1.Services.cs, Form1.LogTail.cs, Form1.Connection.cs |
| 2026-03-08 | **移除 Bridge 服務** — 刪除所有 Bridge 相關：常數 (BRIDGE_URL/SCRIPT/TOKEN)、UI 列、Start/Stop 方法、KillNodeScript、bridgeUp 狀態、KNOWN_PORTS port 3847、SCAN_RANGES 3840-3860 | 全部 partial class |
| 2026-03-08 | **路徑外部化 (.env)** — 所有寫死路徑改為 `.env` 讀取 (LoadEnv/Env 機制)；csproj 加入 CopyToOutputDirectory；新增 OPENCLAW_CLI 指向 `openclaw.mjs` | .env, Form1.cs, OpenClawPanel.csproj |
| 2026-03-08 | **Gateway 啟動修正** — `cmd /c openclaw` → `node openclaw.mjs`（openclaw 未全域安裝）；啟動時載入 OPENCLAW_DOTENV 環境變數 | Form1.Services.cs |
| 2026-03-08 | **Gateway Token 動態讀取** — 從 `openclaw.json` 的 `gateway.auth.token` 讀取，不再寫死 | Form1.cs |
| 2026-03-08 | **需求1：Gateway Log 即時顯示** — Logs tab：RichTextBox (Consolas 9pt)；讀取 `%TEMP%\openclaw\openclaw-YYYY-MM-DD.log`（每日輪替）；JSON 解析 + ANSI strip；FileSystemWatcher + 2s polling；自動捲動 + 5000 行上限 | Form1.LogTail.cs, Form1.UI.cs |
| 2026-03-08 | **需求2：連線資訊面板** — Connection tab：ngrok URL + Copy、LINE webhook URL 自動組合 + Copy、Gateway Token show/hide/copy、Gateway 狀態 (port/PID/uptime) | Form1.Connection.cs, Form1.UI.cs |
| 2026-03-08 | **需求3：ngrok URL 變更提醒** — 偵測 URL 變化 → 黃色 alert banner + LINE Console 快捷按鈕 + 自動切換 Connection tab | Form1.Connection.cs |
| 2026-03-08 | **Config 修正** — 移除 openclaw.json 無效 `mentionOnly` key；複製 ngrok-policy.yml 至 `E:\.openclaw\` 並更新 Bearer token | openclaw.json, ngrok-policy.yml |
| 2026-03-08 | **Dispose 清理** — 加入 FileSystemWatcher/Timer/HttpClient dispose | Form1.Designer.cs |
| 2026-03-08 | 知識庫建立：_AIDocs 初始化、專案掃描完成 | _AIDocs/, CLAUDE.md, memory/ |

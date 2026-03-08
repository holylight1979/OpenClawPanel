# 決策記憶 — 效率與 Token 節省

> 三層分類系統定義見全域 `~/.claude/CLAUDE.md`。

---

## 工程決策

- [固] **發佈格式**: PublishSingleFile + SelfContained=false + RuntimeIdentifier=win-x64。產出只有 `OpenClawPanel.exe` + `.pdb` + `.env`，無碎檔。需要目標機器安裝 .NET 9 Runtime。
- [固] **建置指令**: `dotnet publish -c Release`（非 dotnet build）。VS 內對應「發佈」而非一般建置。
- [固] **所有設定外部化**: 零硬編碼路徑，全從 exe 同層 `.env` 讀取。新增 `OPENCLAW_DOTENV` 指向 OpenClaw 的 .env（含 DISCORD_BOT_TOKEN 等）。

---

## 演化日誌

| 日期 | 記憶 | 變更 |
|------|------|------|
| 2026-03-08 | 發佈格式 | [固] PublishSingleFile, framework-dependent win-x64 |
| 2026-03-08 | 設定外部化 | [固] 移除所有硬編碼路徑 fallback，統一 .env |
| 2026-03-08 | 知識庫建立 | 初始化 _AIDocs 與記憶工作流 |

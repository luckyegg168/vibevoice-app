# VibeVoice

**即時語音輸入法** — Windows 桌面應用，按住快捷鍵說話，自動辨識並輸入文字。

## 功能特色

- 🎙️ 全域快捷鍵錄音（預設 `Ctrl+Alt+V`）
- 🔤 自動辨識語音並注入至任何輸入框
- 🀄 繁體 / 簡體中文轉換
- 📝 標點符號自動補齊
- 🗂️ 歷史記錄管理（SQLite）
- 🔔 系統匣常駐 + 最小化
- 🎨 Fluent Design 深色主題

## 技術棧

| 項目 | 技術 |
|------|------|
| 框架 | .NET 8 WPF |
| UI | WPF-UI 3.0.5 (Fluent Design) |
| 錄音 | NAudio 2.2.1 |
| ASR API | 自架 Whisper API |
| 資料庫 | SQLite (Microsoft.Data.Sqlite) |
| 中文轉換 | Windows LCMapString P/Invoke |
| 文字注入 | SendInput P/Invoke |
| 熱鍵 | NHotkey.Wpf |

## 設定

- **API 端點**: `http://192.168.80.60:8000` (可在設定頁面修改)
- **資料庫**: `%LOCALAPPDATA%\VibeVoice\history.db`

## 編譯

```powershell
cd src
dotnet build
# 發佈單一執行檔
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish
```

## 需求

- Windows 10/11 x64
- .NET 8 Runtime（自包含版本無需安裝）

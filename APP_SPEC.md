# APP_SPEC.md — Application Specification

---

## 專案名稱
VibeVoice — 語音轉文字輸入助手

## 目標描述
一個 Windows 桌面工具，讓使用者透過麥克風錄音，呼叫本地語音辨識 API（http://192.168.80.60:8000）將語音轉為文字，並自動將結果輸入至當前焦點視窗（如 Word、記事本、瀏覽器輸入框等），支援簡繁中文互轉、標點符號插入，以及完整的歷史紀錄管理。

## 目標使用者
需要大量文字輸入、但打字效率有限的中文使用者；包含辦公室工作者、醫療紀錄人員、學生等。

## 核心功能列表
- F-01: 一鍵錄音（快捷鍵 / 懸浮按鈕），放開後自動送出語音至 API 轉錄
- F-02: 轉錄結果自動貼入當前焦點視窗（SendInput / SendKeys 機制，保持游標位置）
- F-03: 簡繁中文互轉（轉錄完成後可切換輸出模式：繁體 / 簡體）
- F-04: 標點符號模式（自動插入中文標點 / 英文標點 / 無標點，三種模式切換）
- F-05: 歷史紀錄面板（列出最近 100 筆轉錄內容，可點選重發、複製、刪除）
- F-06: 發送文字排隊機制（佇列式，防止多次快速觸發導致文字亂序）
- F-07: 系統匣常駐（Tray Icon），可從右鍵選單快速存取主要功能
- F-08: API 連線設定（可自訂 API endpoint、port、timeout）
- F-09: 全域快捷鍵設定（自訂錄音觸發鍵，預設 Ctrl+Alt+V）

## 技術限制
- Language: C# (.NET 8, WPF)
- UI Framework: WPF-UI (Wpf-UI NuGet，Fluent Design)
- Target OS: Windows 10 / 11 (x64)
- 語音 API (VibeVoice-ASR Python Native API v1.0.0):
  - 健康檢查: GET http://192.168.80.60:8000/health → HealthResponse
  - 轉錄:     POST http://192.168.80.60:8000/api/v1/transcribe
              Content-Type: multipart/form-data
              Field: file (WAV / MP3 / FLAC / M4A)
              Query: return_format=parsed|transcription_only|raw, language (optional)
              Response: TranscriptionResponse { status, transcription, processing_time, audio_duration, model_id, timestamp }
  - 模型列表: GET http://192.168.80.60:8000/api/v1/models → ModelInfo[]
  - 效能指標: GET http://192.168.80.60:8000/api/v1/metrics
  - 轉錄結果 transcription 欄位型別:
      * return_format=transcription_only → string（純文字，直接使用）
      * return_format=parsed            → object（含 segments/timestamps，取 text 欄位）
      * return_format=raw               → array（原始 token 列表）
    UI 預設使用 transcription_only，取得 string 後直接顯示與注入
- No external database（使用本機 SQLite via Microsoft.Data.Sqlite 儲存歷史紀錄）
- 簡繁轉換: OpenCCSharp NuGet（opencc-csharp）
- 文字注入: Windows SendInput API（user32.dll P/Invoke），透過 InputSimulator 或自實作
- 錄音: NAudio NuGet（WaveInEvent → WAV byte stream）
- 封裝: 單一可攜式 .exe（self-contained, win-x64 publish）

## 非功能需求
- 主視窗啟動時間 < 2 秒
- 錄音觸發至 API 回傳延遲顯示 < 100ms（UI 反應，非 API 延遲）
- 發送文字至焦點視窗延遲 < 50ms（排隊機制下每字元間距 ≤ 5ms）
- 支援 Windows 10 / 11（不支援 macOS / Linux）
- API 超時預設 10 秒，逾時顯示友善錯誤訊息
- 歷史紀錄最多保留 1000 筆，超過自動刪除最舊紀錄
- 簡繁轉換不影響英文、數字、符號
- 支援中文、英文混合轉錄結果
- 視窗可最小化至系統匣，不占用工作列
- 設定值持久化至 appsettings.json

## 驗收標準 (Acceptance Criteria)
- AC-01: 按下全域快捷鍵開始錄音，UI 顯示「錄音中...」狀態指示，放開後呼叫 API，轉錄結果顯示於預覽區
- AC-02: 轉錄結果自動貼入當前焦點視窗（測試：記事本），文字完整且無亂序
- AC-03: 切換「繁體」模式時，轉錄的簡體字自動轉為繁體後再貼入
- AC-04: 切換「英文標點」模式時，句末自動補上「.」；切換「中文標點」時補「。」；「無標點」模式不添加
- AC-05: 歷史紀錄面板列出最近轉錄，點選任一筆可重新發送至焦點視窗
- AC-06: 快速連按快捷鍵兩次，兩段文字按順序正確貼入，不亂序
- AC-07: API 連線失敗時（server 不可達），顯示錯誤通知，不 crash，歷史紀錄記錄失敗事件
- AC-08: 重啟程式後歷史紀錄、設定值完整保留
- AC-09: 系統匣右鍵選單可開啟主視窗、切換簡繁模式、退出程式
- AC-10: 在設定頁修改 API endpoint 後，下次錄音使用新設定，無需重啟

## 排除範圍 (Out of Scope)
- 不包含雲端 API（Azure / Google Speech）整合，僅使用本地 API
- 不包含即時串流語音辨識（Streaming ASR），僅支援錄音後一次性送出
- 不包含語者辨識（Speaker Diarization）
- 不包含多語言自動偵測（語言由 API 端決定）
- 不包含 macOS / Linux 版本
- 不包含行動端 App
- 不包含雲端同步歷史紀錄
- 不包含語音合成（TTS）功能
- 不包含多使用者帳號管理

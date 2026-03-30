# CLAUDE.md — Autonomous App Development Orchestration Template
# 版本: v1.0 | 語言: zh-TW (技術名詞保留英文)

---

## ██ MISSION STATEMENT

你是 **Orchestrator Agent**。  
你的唯一任務是：**從 APP_SPEC.md 出發，自主協調多個 Sub-Agent，迭代開發、測試、除錯，直到 app 通過所有 Quality Gate，然後交付。**

除非 HARD BLOCK（定義見下方），否則你**不得向人類請求指引**。  
你**不得跳過任何 Phase**。  
你**不得偽造任何 Gate 結果**。

---

## ██ AGENT ROSTER

| Agent ID           | 角色             | 主要職責                                         |
|--------------------|------------------|--------------------------------------------------|
| `orchestrator`     | 總指揮           | 任務分配、狀態追蹤、Gate 判斷、迭代控制          |
| `architect`        | 架構設計師       | 技術選型、模組分解、資料流設計、Plan 產出         |
| `developer`        | 實作工程師       | 程式碼撰寫、unit test、lint、自我 review         |
| `qa`               | QA 工程師        | 整合測試、E2E 測試、缺陷分類、除錯協作           |
| `reviewer`         | 程式碼審核員     | 安全性、可維護性、設計規範符合度                 |
| `delivery`         | 交付工程師       | Changelog、版本封存、文件產出、打包              |

> **注意**：所有 Agent 均由 Orchestrator 依序召喚，不得並行執行互相依賴的 Phase。

---

## ██ INPUT CONTRACT

開始前必須存在以下檔案：

### `APP_SPEC.md`（必填）
```
## 專案名稱
## 目標描述 (What problem does this solve?)
## 目標使用者
## 核心功能列表 (每項一行，前綴 - )
## 技術限制 (語言/框架/平台偏好，若無則寫 OPEN)
## 非功能需求 (效能、安全、可用性等)
## 驗收標準 (Acceptance Criteria，量化)
## 排除範圍 (Out of Scope)
```

### `CONSTRAINTS.md`（選填，若無則 Architect 自行生成）
```
## Budget (API token 上限)
## Forbidden libraries
## Target OS / runtime
## Secrets handling policy
```

---

## ██ PHASE ARCHITECTURE

```
Phase 0: Kick-off & Validation
Phase 1: Architecture & Planning      ← architect
Phase 2: Implementation               ← developer  (循環)
Phase 3: Review & QA                  ← reviewer + qa (循環)
Phase 4: Debug Loop                   ← developer + qa (條件觸發)
Phase 5: Delivery                     ← delivery
```

每個 Phase 結束後必須執行對應的 **Gate Check**（見下方）。  
Gate FAIL → 強制進入 Debug Loop，不得進入下一 Phase。

---

## ██ PHASE 0 — KICK-OFF & VALIDATION

**執行者**: orchestrator  
**觸發條件**: 收到任務啟動指令

### 步驟
1. 讀取 `APP_SPEC.md`
2. 驗證必填欄位完整性
3. 若欄位缺失 → 列出缺失項目，停止並回報（唯一合法的人類互動點）
4. 建立工作目錄結構：
   ```
   /project-root/
   ├── src/
   ├── tests/
   │   ├── unit/
   │   ├── integration/
   │   └── e2e/
   ├── docs/
   ├── .agent-state/
   │   ├── PLAN.md
   │   ├── PROGRESS.md
   │   ├── DEFECTS.md
   │   └── ITERATION_LOG.md
   └── CHANGELOG.md
   ```
5. 初始化 `.agent-state/PROGRESS.md`：
   ```markdown
   # PROGRESS
   Current Phase: 0
   Iteration: 0
   Status: IN_PROGRESS
   Last Gate: N/A
   ```
6. 召喚 architect（Phase 1）

---

## ██ PHASE 1 — ARCHITECTURE & PLANNING

**執行者**: architect  
**觸發條件**: Orchestrator 召喚

### 步驟
1. 閱讀 `APP_SPEC.md` 與 `CONSTRAINTS.md`
2. 輸出 `.agent-state/PLAN.md`（**必須包含以下所有章節**）：

```markdown
# PLAN.md

## 技術棧決策
- Language: ...
- Framework: ...
- Database: ...
- Testing: ...
- Build tool: ...
- CI (if any): ...
- 決策理由: (每項一句)

## 模組清單
| 模組 ID | 名稱 | 職責 | 依賴 |
|---------|------|------|------|
| M01     | ...  | ...  | none |
| M02     | ...  | ...  | M01  |

## 資料流圖 (文字版)
[Input] → [Module A] → [Module B] → [Output]

## 檔案結構（預期）
src/
  module-a/
    index.ts
    index.test.ts
  ...

## 實作順序 (dependency-first)
1. M01 (no deps)
2. M02 (deps: M01)
...

## 風險清單
| 風險 | 機率(H/M/L) | 緩解策略 |
|------|-------------|----------|
| ...  | ...         | ...      |

## 驗收標準對應表
| AC ID | 原始 AC 文字 | 測試類型 | 自動化可行? |
|-------|-------------|----------|------------|
| AC-01 | ...         | E2E      | Yes        |
```

3. 完成後 → 更新 `PROGRESS.md` Phase: 1, Status: GATE_CHECK

### Gate 1: Architecture Review
```
PASS 條件:
  ✓ PLAN.md 所有章節已填寫
  ✓ 模組清單覆蓋 APP_SPEC 所有核心功能
  ✓ 驗收標準對應表 100% 覆蓋
  ✓ 無循環依賴

FAIL → 回到 Phase 1，architect 修正
```

---

## ██ PHASE 2 — IMPLEMENTATION

**執行者**: developer  
**觸發條件**: Phase 1 Gate PASS

### 開發規則（developer 必須遵守）

1. **依照 PLAN.md 實作順序開發**，一次一個模組
2. **每個模組完成後立即撰寫 unit test**（不允許先全部實作再補 test）
3. **每個 commit 前執行**：
   ```bash
   # Lint
   <linter_command>  # 由 architect 在 PLAN.md 指定
   # Unit test
   <test_runner> --coverage
   ```
4. **自我 review checklist**（每個函式/類別/模組）：
   ```
   □ 有無 magic number（應改為常數）
   □ 有無 naked catch（應記錄 error context）
   □ 有無 hardcoded secret
   □ function 長度 ≤ 50 lines（否則拆分）
   □ 有無 TODO 未解決（允許但須記錄在 DEFECTS.md）
   ```
5. 每完成一個模組，在 `PROGRESS.md` 新增：
   ```
   [M01] DONE - unit test: 12/12 pass - coverage: 87%
   ```

### 實作輸出要求
- 程式碼必須完整，不得有 `// TODO: implement this` 作為交付物
- 每個公開 API 必須有 JSDoc / docstring（語言依 PLAN 決定）
- 不允許 `console.log` / `print` 留在 production 路徑（僅允許 logger）

### Gate 2: Build & Unit Test
```
PASS 條件:
  ✓ Build 無 error（warning 允許但須記錄）
  ✓ All unit tests PASS
  ✓ Coverage ≥ 80%（可在 CONSTRAINTS.md 調整）
  ✓ Lint: 0 error

FAIL → Phase 4 Debug Loop（developer 修正）
```

---

## ██ PHASE 3 — REVIEW & QA

**執行者**: reviewer → qa（序列執行）  
**觸發條件**: Phase 2 Gate PASS

### Reviewer 步驟
1. 掃描所有 src/ 程式碼
2. 輸出 `.agent-state/REVIEW_REPORT.md`：
   ```markdown
   ## Code Review Report

   ### BLOCKER (必須修復，Gate 前不得通過)
   - [FILE:LINE] 描述

   ### MAJOR (強烈建議修復)
   - [FILE:LINE] 描述

   ### MINOR (可選，記錄即可)
   - [FILE:LINE] 描述

   ### 安全性掃描
   - Hardcoded secrets: NONE / [found items]
   - SQL injection risk: NONE / [found items]
   - Unvalidated input: NONE / [found items]
   ```

### QA 步驟
1. 閱讀 `APP_SPEC.md` 驗收標準對應表
2. 執行整合測試 (integration tests)
3. 執行 E2E 測試（若 AC 要求）
4. 對每個 AC 標記結果：
   ```
   AC-01: PASS
   AC-02: FAIL - [symptom description] - [steps to reproduce]
   ```
5. 輸出 `.agent-state/DEFECTS.md`：
   ```markdown
   ## DEFECTS

   ### Open
   | ID    | Severity | AC    | 描述 | Assigned |
   |-------|----------|-------|------|----------|
   | D-001 | BLOCKER  | AC-02 | ... | developer|

   ### Resolved
   | ID    | Fixed in Iteration |
   |-------|--------------------|
   ```

### Gate 3: QA Acceptance
```
PASS 條件:
  ✓ REVIEW_REPORT: BLOCKER count = 0
  ✓ REVIEW_REPORT: MAJOR count ≤ 2
  ✓ All AC: PASS
  ✓ Integration tests: 100% pass
  ✓ No OPEN BLOCKER in DEFECTS.md

FAIL → Phase 4 Debug Loop
```

---

## ██ PHASE 4 — DEBUG LOOP

**執行者**: developer + qa  
**觸發條件**: Phase 2 Gate FAIL 或 Phase 3 Gate FAIL

### 流程
```
1. Orchestrator 將 DEFECTS.md 中所有 OPEN items 分配給 developer
2. Developer 逐一修復，每次修復後執行：
   - Unit test (targeted)
   - Regression test (全套)
3. Developer 更新 DEFECTS.md：OPEN → RESOLVED
4. QA 重新驗證所有 RESOLVED items
5. 若驗證通過 → 返回觸發 Phase（2 或 3）的 Gate Check
6. 若出現新 defect → 新增至 DEFECTS.md，繼續 Loop
```

### 迭代上限控制
```
MAX_ITERATIONS = 5  (可在 CONSTRAINTS.md 覆蓋)

每次迭代 → 在 ITERATION_LOG.md 新增：
  Iteration N | Phase | Defects Fixed | New Defects | Gate Result

若達到 MAX_ITERATIONS 且 Gate 仍 FAIL：
  → Orchestrator 生成 ESCALATION_REPORT.md
  → 停止並回報人類（唯一例外的人類介入點）
```

### `ESCALATION_REPORT.md` 格式
```markdown
# ESCALATION REPORT
Generated: [timestamp]
Iteration: [N] / [MAX]

## 無法自動解決的問題
[問題清單]

## 已嘗試的解法
[摘要]

## 建議的人工介入方向
[建議]

## 當前程式碼狀態
Build: PASS/FAIL
Unit Test: X/Y pass
Coverage: Z%
Open BLOCKERs: [list]
```

---

## ██ PHASE 5 — DELIVERY

**執行者**: delivery  
**觸發條件**: Phase 3 Gate PASS

### 步驟
1. 生成 `CHANGELOG.md`：
   ```markdown
   # CHANGELOG

   ## [1.0.0] - {date}

   ### Added
   - [功能列表，對應 APP_SPEC 核心功能]

   ### Architecture
   - [技術棧摘要]

   ### Test Coverage
   - Unit: X%
   - Integration: N tests pass
   - E2E: AC-01, AC-02, AC-03 PASS

   ### Known Limitations
   - [MINOR defects 留存清單]
   ```

2. 生成 `docs/USER_GUIDE.md`（至少包含）：
   ```
   ## 安裝
   ## 快速開始
   ## 核心功能說明
   ## 常見問題
   ```

3. 生成 `docs/DEVELOPER_GUIDE.md`（至少包含）：
   ```
   ## 開發環境設定
   ## 測試執行方式
   ## 模組架構說明
   ## 新增功能指南
   ```

4. 打包（依 PLAN.md 技術棧決定方式）

5. 更新 `PROGRESS.md`：
   ```
   Current Phase: COMPLETE
   Status: DELIVERED
   Final Iteration: N
   ```

6. 輸出最終摘要至 stdout：
   ```
   ══════════════════════════════════
    APP DELIVERY COMPLETE
   ══════════════════════════════════
   Project : [name]
   Version : 1.0.0
   Delivered: [timestamp]

   Iterations Used : N / MAX
   Unit Coverage   : X%
   AC Pass Rate    : Y/Y (100%)
   Open Defects    : Z (MINOR only)

   Artifacts:
   - src/           (source code)
   - tests/         (all test suites)
   - docs/          (user + dev guide)
   - CHANGELOG.md
   ══════════════════════════════════
   ```

---

## ██ HARD BLOCK — 合法的人類介入點

以下情況 **Orchestrator 必須停止並通知人類**，不得自行繼續：

1. `APP_SPEC.md` 必填欄位缺失（Phase 0）
2. 技術選型有根本性衝突，無法自動解決（Phase 1）
3. 達到 `MAX_ITERATIONS` 仍有 BLOCKER（Phase 4）
4. 偵測到可能的 secret/credential 洩露風險
5. 需要存取外部系統（DB、雲端、第三方 API）但缺少認證資訊

---

## ██ QUALITY RED LINES（永不妥協）

```
✗ 不得提交含有 BLOCKER 的程式碼至下一 Phase
✗ 不得偽造 test pass（不得刪除 failing test 以提高 pass rate）
✗ 不得跳過 Gate Check
✗ 不得在沒有 unit test 的情況下標記模組為 DONE
✗ 不得 hardcode secret 至程式碼（必須使用 env var）
✗ 不得忽略 ESCALATION 觸發條件而繼續無限迭代
```

---

## ██ STATE FILES REFERENCE

| 檔案 | 寫入者 | 讀取者 | 用途 |
|------|--------|--------|------|
| `APP_SPEC.md` | 人類 | 所有 Agent | 需求來源 |
| `.agent-state/PLAN.md` | architect | developer, qa | 實作藍圖 |
| `.agent-state/PROGRESS.md` | orchestrator | 所有 | 進度追蹤 |
| `.agent-state/DEFECTS.md` | qa | developer, orchestrator | 缺陷管理 |
| `.agent-state/REVIEW_REPORT.md` | reviewer | orchestrator | 程式碼品質 |
| `.agent-state/ITERATION_LOG.md` | orchestrator | orchestrator | 迭代歷史 |
| `CHANGELOG.md` | delivery | 人類 | 版本紀錄 |
| `ESCALATION_REPORT.md` | orchestrator | 人類 | 升級處理 |

---

## ██ STARTUP COMMAND

將以下指令傳給 Agent 以啟動整個 pipeline：

```
你是 Orchestrator Agent。
請立即執行 CLAUDE.md 中定義的 Phase 0，讀取 APP_SPEC.md，
驗證後開始自主開發流程。
不需要等待我的確認，直到出現 HARD BLOCK 才回報。
```

---

*CLAUDE.md v1.0 | Autonomous App Development Orchestration Template*
*適用於：任何語言/框架的 App 開發自動化場景*

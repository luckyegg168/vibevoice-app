# CONSTRAINTS.md — Agent Constraints (選填)
# 若不提供此檔案，Architect 將使用合理預設值

---

## MAX_ITERATIONS
5
# 達到此次數若仍有 BLOCKER，Orchestrator 將停止並生成 ESCALATION_REPORT

## Coverage Threshold
80
# Unit test coverage 最低門檻 (%)

## Forbidden Libraries
# 列出禁止使用的套件（每行一個）
# 例: lodash

## Target Runtime
# 例: Node.js 20+, Python 3.11+, Rust stable
OPEN

## Secrets Handling Policy
env_var_only
# 選項: env_var_only | config_file | vault

## Build Output
# 最終打包格式
# 例: binary | docker_image | npm_package | python_wheel | zip
OPEN

## Extra Quality Rules
# 任何額外的程式碼品質規定（每行一條）
# 例: No any type in TypeScript
# 例: All public functions must have docstring

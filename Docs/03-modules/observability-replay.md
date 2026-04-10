# Observability & Replay

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-09
- Related:
  - [Runtime Lifecycle](../02-architecture/runtime-lifecycle.md)
  - [Memory & Retrieval](./memory-retrieval.md)
  - [Week7 Acceptance Runbook](./week7-acceptance-runbook.md)

## 文档目的

这份文档描述事件模型、脱敏规则、回放用途和 Week7 之后新增的关键事件。它是 observability 与 replay 的唯一权威说明。

## 为什么事件是第一公民

产品级 Agent 如果不能回答“为什么这么做”，就无法真正交付。SkAgent 的运行链路要求把意图识别、检索决策、计划、工具调用、失败、修复、后置写入都变成事件，从而支持：

- 调试
- 回放
- 审计
- 指标分析
- Demo 展示

## 事件分层

### Runtime 事件

- run_started
- prompt_composed
- plan_generated
- step_started / step_finished
- run_completed / run_failed

### Memory 事件

- intent_classified
- retrieval_plan_built
- vector_query_executed
- memory_retrieved_long_term
- memory_fused
- vector_upserted
- fact_upserted
- fact_conflict
- profile_updated
- profile_update_skipped
- safety_policy_applied

### 平台与安全事件

- model_selected
- daily_job_started
- suggestion_saved
- daily_job_finished
- daily_job_failed
- event_payload_redacted
- 后续预留：external_call_started / finished / blocked

## 事件载荷规范

### 通用字段

- `runId`
- `conversationId`
- `timestamp`
- `eventType`
- `payload`
- `correlationId`（建议保留）

### Week7 关键 payload

#### `intent_classified`

- intents[]
- confidence
- signals

#### `retrieval_plan_built`

- routes[]
- budgets
- topK
- rewriteUsed
- rationale

#### `vector_query_executed`

- queryHash
- filters
- topK
- latencyMs
- scoreRange

#### `memory_retrieved_long_term`

- queryHash
- candidates
- kept
- budgetChars
- dedupeCount
- truncateReason

#### `memory_fused`

- byRouteCounts
- totalItems
- budgetUsed
- conflictsResolved

#### `vector_upserted`

- runId
- conversationId
- chunks
- chars
- model
- latencyMs
- dedupeCount

### Week8 关键 payload

#### `daily_job_started`

- date
- personaName
- conversationId

#### `suggestion_saved`

- date
- runId
- hash
- chars
- eventLogPath

#### `daily_job_finished`

- date
- runId
- created

#### `daily_job_failed`

- date
- runId
- error

## 脱敏规则

- Tool args 应按 allowlist / denylist 脱敏，不把敏感参数原样写入事件。
- profile 中涉及 PII 的字段必须遮蔽或摘要化。
- 如果某次事件发生了脱敏，记录 `event_payload_redacted` 说明处理过哪些字段。

## Replay 用途

### 面向开发与调试

- 还原一次 run 的 Prompt、Plan、Step、Tool、Memory 决策。
- 找出错误是来自 Planner、Tool 还是 Memory Fusion。

### 面向产品演示

- 展示“为什么检索、为什么裁剪、为什么修计划”。
- 展示产品不是黑盒，而是可解释的运行时。

### 面向合规与审计

- 对高风险意图、外部调用、医疗类提示等行为保留证据链。

## 当前状态

- 事件模型已覆盖 Runtime 主链路、Week7 记忆增强链路和 Week8 Daily Suggestion foundation，并已通过真实环境验收。
- JSONL 事件日志已在真实 daily run 中充当 replay 指针并通过验收。
- UI 回放、事件筛选和审计视图将在 Week9 继续加强。

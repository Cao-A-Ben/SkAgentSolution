# Daily Suggestions

- Status: Week8 Foundation In Progress
- Owner: Ben + Codex
- Last Updated: 2026-04-09
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](./observability-replay.md)
  - [Memory & Retrieval](./memory-retrieval.md)
  - SQL: [20260409_week8_daily_suggestions.sql](../sql/20260409_week8_daily_suggestions.sql)

## 文档目的

这份文档是 Week8 `Daily Suggestion Job` 的专题说明。它描述建议生成的目标、数据流、运行边界、存储结构和回放指针，避免“每天建议”变成绕开 Runtime 的旁路能力。

## Week8 目标

- 每天生成一条建议。
- 建议可落库。
- 建议与 `runId`、`promptHash`、`eventLogPath` 关联。
- 支持手动触发。
- 后台调度默认关闭，但具备启用能力。
- 同一天重复调用不重复生成。

## 当前实现状态

### 已完成

- `SuggestionRecord`、`ISuggestionStore`、`IConversationScopeResolver` 契约已建立。
- `DailySuggestionService` 已复用现有 `RunPreparationService + PromptComposer` 链路。
- `PromptTarget.Daily` 已接入 `model_selected + prompt_composed(target=daily)`。
- Postgres 存储 `SqlSuggestionStore` 已实现，内存回退实现已实现。
- `POST /api/suggestions/daily:run` 与 `GET /api/suggestions` 已接入。
- `JsonlRunEventSink + JsonlRunEventLogFactory` 已接入，建议记录会保存 `eventLogPath`。
- `DailySuggestionJob` 已作为 Host 层后台任务接入，默认 `Enabled=false`。
- 单测已覆盖“同一天重复调用不重复生成”。

### 尚待验证

- 真实数据库执行 Week8 SQL 后的端到端 API 验收。
- 手动触发后的真实 suggestion 文本质量验收。
- 后台调度开启后的时间触发验收。

## 数据流

1. 解析目标日期、persona 和 conversation scope。
2. 若当天建议已存在，则直接返回现有记录。
3. 创建独立的 Daily run context，并绑定 JSONL 事件日志。
4. 加载最近短期会话、画像、persona 和 retrieval plan。
5. 调用 `RunPreparationService` 生成 daily prompt。
6. 通过文本生成服务生成建议正文。
7. 保存 `daily_suggestions` 记录。
8. 发出 `daily_job_started -> prompt_composed(target=daily) -> suggestion_saved -> daily_job_finished` 事件链。

## 存储模型

`daily_suggestions` 的最小字段：

- `suggestion_date`
- `suggestion`
- `run_id`
- `conversation_id`
- `persona_name`
- `profile_hash`
- `prompt_hash`
- `created_at`
- `event_log_path`

设计原则：

- `daily_suggestions` 是建议结果表，不是原始事件表。
- 事件仍写到 JSONL，以保持 replay 指针和 SuggestionRecord 解耦。
- 唯一键按 `suggestion_date + persona_name`，保证同一天不会无限追加。

## API

### `POST /api/suggestions/daily:run`

用途：手动触发当天或指定日期的建议生成。

请求体：

```json
{
  "date": "2026-04-09",
  "personaName": "default",
  "conversationId": "optional-conversation-id"
}
```

返回：

- `date`
- `suggestion`
- `runId`
- `conversationId`
- `personaName`
- `promptHash`
- `profileHash`
- `eventLogPath`
- `created`

### `GET /api/suggestions`

用途：读取最近建议记录列表，供后续 UI / replay 入口使用。

## 调度策略

- 默认 `Enabled=false`，避免开发环境一启动就自动落库。
- `RunOnStartupIfMissing=true`，当显式开启后台任务后，如果当天缺失记录，可在启动时补跑一次。
- `UseLatestConversation=true`，当前演示阶段默认用最近活跃会话作为建议上下文来源。

## 事件与回放

Week8 关键事件：

- `daily_job_started`
- `prompt_composed(target=daily)`
- `suggestion_saved`
- `daily_job_finished`
- `daily_job_failed`

回放原则：

- 以 `SuggestionRecord.runId` 作为主键定位一次 daily run。
- 以 `eventLogPath` 找到完整事件 JSONL。
- 以 `promptHash` 确认当次提示词版本。

## 当前边界

### 已明确不做

- 暂不做复杂 dead-letter 队列。
- 暂不做通知渠道推送。
- 暂不做多用户调度中心。
- 暂不做建议质量打分和 rerank。

### 后续可扩展

- Week8.5 接入 `ModelPurpose.Daily` 的独立模型策略。
- Week9 在 UI 中展示建议历史和 replay 入口。
- Week11 之后可把 daily suggestion 扩展为 MCP / tools 增强的主动任务。

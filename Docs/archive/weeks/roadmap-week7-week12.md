> Status: Archived legacy document

> See the new main docs under `Docs/01-roadmap`, `Docs/02-architecture`, and `Docs/03-modules`.

# Roadmap Week7 → Week12

## Week7（已落地主轴）
- Intent Router + RetrievalPlan
- pgvector Long-term Memory
- 多路融合（short/working/facts/profile/vector）
- 关键事件链可回放

## Week8
- DailySuggestionJob（落库 + 重试 + 回放 runId）
- suggestion 事件体系（started/finished/failed/retry/saved）

## Week8.5
- 多模型路由（planner/chat/embedding）
- 模型选择事件（model_selected / model_call_finished）

## Week9
- 回放 UI（timeline + step tree + prompt panel）
- 语音闭环（STT -> runtime -> TTS）

## Week10
- Reviewer + Repair Plan（非盲重试）
- diff 可回放事件

## Week11
- MCP 外部系统工具化
- allowlist + scope + 审计拦截

## Week12
- 领域 Skills（中医/技术）
- 合规模板化与 Demo 收官（可复现、可审计）

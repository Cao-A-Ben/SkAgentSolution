# Memory & Retrieval

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-08
- Related:
  - [Runtime Lifecycle](../02-architecture/runtime-lifecycle.md)
  - [Observability & Replay](./observability-replay.md)
  - [Week7 Acceptance Runbook](./week7-acceptance-runbook.md)
  - SQL: [20260326_week7_pgvector.sql](../sql/20260326_week7_pgvector.sql)

## 文档目的

这份文档集中描述 Week7 之后的记忆体系。它是 Memory、Intent Router、RetrievalPlan、Fusion、pgvector 和长期写入链路的权威说明。

## 记忆分层

| 层级 | 作用 | 时间尺度 | 主要来源 |
| --- | --- | --- | --- |
| Short-term | 最近几轮对话上下文 | 当前会话 | Conversation turns |
| Working | 当前 run 执行过程中的临时工作集 | 单次运行 | Planner / Executor / Tools |
| Facts | 稳定事实与显式偏好 | 跨 run | 提取器、用户明确陈述 |
| Profile | 对用户 / 角色的聚合画像 | 跨 run | Fact 聚合、策略更新 |
| Vector | 长期文本片段与语义召回 | 跨天 / 跨会话 | Chunk + Embedding + pgvector |

## Intent Router

### 角色定位

- Intent Router 是 Planner 的上游供给层。
- 它不产步骤，只决定检索路线、预算、是否澄清、是否触发安全策略。

### 输出

- `RetrievalIntent`：支持 multi-label。
- `RetrievalPlan`：包含 routes、topK、budgets、rewrite、needClarification、safetyPolicy、rationale。

### 典型意图

- `chitchat`
- `recall`
- `tool_needed`
- `goal_tracking`
- `health_sensitive`

## Query Rewriting

- 目的：把自然表达重写成更适合长期检索的 query。
- 例子：
  - “我之前说过要做什么？” -> “user goals preferences previous commitments”
  - “之前有没有提到过睡眠不好？” -> “past mentions sleep issues health notes”
- 可选地对一个请求生成 2 到 3 个子查询并行召回。

## Retrieval Fusion

### 输入路由

- short
- working
- facts
- profile
- vector
- 预留 web / tool

### 核心规则

- 先按 route 分配预算，再统一裁剪。
- 先去重，再做冲突消解。
- 默认优先级：`facts > recent user statement > vector old`。
- 对 health-sensitive 请求，强制注入相关禁忌和免责声明。

### 输出

- 融合后的 memory items。
- 面向 Prompt 的解释性摘要。
- 事件级解释：为什么召回、为什么裁剪、为什么跳过。

## Facts / Profile 双轨

### Facts

- 结构：key / value / confidence / source / ts。
- 目标：保存明确且可定位来源的稳定事实。

### Profile

- 目标：把 facts 聚合成更适合消费的用户画像视图。
- Profile 不是事实源头，而是 facts 的受控投影。

### 冲突策略

- 新事实和旧事实冲突时先记录 `fact_conflict`。
- Profile 只有在策略允许时更新，否则记录 `profile_update_skipped`。

## Long-term Memory 写入链路

1. `run_completed`
2. 提取可保留内容
3. Chunk
4. Embedding
5. `content_hash` 去重
6. Upsert 到 pgvector
7. 记录 `vector_upserted`

## pgvector 落地

### 当前状态

- SQL 脚本已执行。
- `memory_chunks` 已开始承载长期记忆片段。
- 向量写入、conversation 过滤和 recall 验收均已在真实环境通过。

### 表结构要点

- `memory_chunks`
- `content_hash` 唯一
- `embedding vector(N)`
- metadata 用 `jsonb`
- `(conversation_id, ts)` 辅助索引

### 查询策略

- 核心召回语句使用 `ORDER BY embedding <-> :queryEmbedding LIMIT :topK`
- conversation scope 优先，其次再扩展到更大 user scope

## Week7 当前实施状态

### 已完成

- 契约统一：`ILongTermMemory`、`IVectorStore`、`VectorRecord / VectorQuery / VectorHit`
- RunPreparation 接入 Intent Router 和 RetrievalPlan
- 多路融合、事实 / 画像更新、向量入库编排
- `recent_history -> recall_summary_built -> deterministic recall output` 已收口
- 代表性输出已稳定为：`你刚刚说了“你好啊”。`

### 代表性事件链

- `intent_classified`
- `retrieval_plan_built`
- `recent_history_retrieved`
- `recall_summary_built`
- `memory_fused`
- `run_completed`
- `vector_upserted`

### 当前结论

- Week7 的 Memory & Retrieval 已达到可演示、可复现、可审计状态。
- 下一步进入 Week8 的 Daily Suggestion Job。

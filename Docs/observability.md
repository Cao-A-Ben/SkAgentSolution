# Observability（Week7 Event Schema）

## Envelope
所有事件使用统一结构：
- `runId`
- `seq`
- `ts`
- `type`
- `payload`

## Week7 必须事件
- `intent_classified`
  - payload: `intents[]`, `confidence`, `signals[]`
- `retrieval_plan_built`
  - payload: `routes[]`, `budgets`, `topK`, `rewriteUsed`, `needClarification`, `safetyPolicy`, `rationale`
- `vector_query_executed`
  - payload: `queryHash`, `filters`, `topK`, `latencyMs`, `scoreRange`
- `memory_retrieved_long_term`
  - payload: `queryHash`, `candidates`, `kept`, `budgetChars`, `dedupeCount`, `truncateReason`
- `memory_fused`
  - payload: `byRouteCounts`, `totalItems`, `budgetUsed`, `conflictsResolved`
- `vector_upserted`
  - payload: `runId`, `conversationId`, `chunks`, `chars`, `model`, `latencyMs`, `dedupeCount`
- `fact_upserted` / `fact_conflict` / `profile_updated` / `profile_update_skipped`
- `safety_policy_applied`
  - payload: `policyId`, `reason`

## 常规事件（沿用）
- `run_started` / `plan_created` / `step_started` / `step_completed` / `step_failed`
- `tool_invoked` / `tool_completed`
- `reflection_triggered` / `retry_scheduled` / `retry_skipped`
- `run_completed` / `run_failed`

## 脱敏原则
- tool 参数仅输出预览与 allowlist 字段。
- 不输出密钥与完整敏感 prompt。
- profile/fact 输出时对 PII 做 mask（后续 Week9 强化）。

> Status: Archived legacy document

> See the new main docs under `Docs/01-roadmap`, `Docs/02-architecture`, and `Docs/03-modules`.

# Runtime Lifecycle（Week7）

## 单次运行主链路
1. `run_started`
2. 加载 `recent_turns` 与 `profile`
3. `RunPreparationService.PrepareAsync`
4. `intent_classified` + `retrieval_plan_built`
5. `MemoryOrchestrator.BuildAsync`（多路召回 + 融合）
6. `prompt_composed(target=planner)`
7. `plan_created`
8. `PlanExecutor.ExecuteAsync`（`step_*`、`tool_*`、`reflection_*`）
9. `run_completed` 或 `run_failed`
10. `LongTermMemoryService.PersistRunAsync`（仅 completed）

## RunPreparation 阶段（新增）
- Persona 选择并写入 state。
- Intent Router 输出 `RetrievalIntent + RetrievalPlan`。
- MemoryOrchestrator 读取 RetrievalPlan 执行分路召回。

## Memory 编排阶段（新增）
- QueryRewriter 对 recall/health 意图生成 1~3 个检索查询。
- 向量召回产出 `vector_query_executed`。
- 融合产出 `memory_retrieved_long_term` 与 `memory_fused`。
- health-sensitive 触发 `safety_policy_applied`。

## 对外接口
- `POST /api/agent/run`
- `POST /api/agentstream/run`

两者共用同一 Runtime 主链路，区别只在是否注入 SSE 事件 sink。

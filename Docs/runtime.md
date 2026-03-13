# Runtime
> Status: Updated after refactor
>
> 本文档描述当前实现中的一次 Run 生命周期，以及非流式与 SSE 如何共用执行主链路。
>
> Related:
> - `Docs/architecture.md`
> - `Docs/observability.md`
> - `Docs/diagrams/runtime-sequence.mmd`

## 1. Run 生命周期（当前实现）

单次运行主流程：
1. `run_started`
2. 读取上下文（短期记忆 / 用户画像 / persona）
3. `plan_created`
4. step 循环执行（agent/tool + 可选 reflection retry）
5. `run_completed` 或 `run_failed`
6. 提交短期记忆、更新画像（运行后处理）

说明：当前实现没有单独发 `plan_started` 事件。

## 2. 组件责任边界

### 2.1 `AgentRuntimeService`
- 创建 `AgentContext` 与 `AgentRunContext`
- 调用 `IPlanner.CreatePlanAsync(PlanRequest)`
- 调用 `PlanExecutor.ExecuteAsync(run)`
- 负责 run 级事件起点（`run_started`）
- 负责执行完成后的记忆与画像写回

### 2.2 `PlannerAgent`
- 根据 `PlannerHint + Profile + RecentTurns + ToolCatalog` 生成 `AgentPlan`
- 输出到 `run.Plan`，并由 Runtime 发出 `plan_created`

### 2.3 `PlanExecutor`
- 顺序执行 `PlanStep`
- `kind=agent`：通过 `IStepRouter` 路由到 `IAgent`
- `kind=tool`：通过 `IToolInvoker` 执行工具
- 失败时调用 `IReflectionAgent` 给出 retry 决策
- 发出 `step_*` / `tool_*` / `reflection_*` / `retry_*` / `run_completed|run_failed`

## 3. 非流式与流式入口

### 3.1 非流式
- API：`POST /api/agent/run`
- 控制器：`AgentController`
- 返回：`AgentRunResponse`

### 3.2 流式（SSE）
- API：`POST /api/agentstream/run`
- 控制器：`AgentStreamController`
- 通过 `SseRunEventSink` 写 `text/event-stream`

### 3.3 共用链路
- 两个入口都调用 `AgentRuntimeService.RunAsync(...)`
- 差异仅在 `eventSink`：
  - 非流式：默认空 sink（不向客户端推送事件）
  - 流式：传入 `SseRunEventSink`

## 4. SSOT 数据（`AgentRunContext`）

核心字段：
- 标识：`RunId`、`ConversationId`
- 输入：`UserInput`、`Root`
- 计划与执行：`Plan`、`Steps`、`Status`、`FinalOutput`
- 共享状态：`ConversationState`
- 记忆与工具：`RecentTurns`、`ToolCalls`
- 事件与重试：`EventSeq`、`StepRetryCounts`、`EventSink`

## 5. 失败与重试

- Step 失败时会触发：
  - `step_failed`
  - `reflection_triggered`
  - 决策后发 `retry_scheduled` 或 `retry_skipped`
- 达到上限或不可重试时，Run 进入失败态并发 `run_failed`
- 单步重试上限：`MaxRetriesPerStep = 3`

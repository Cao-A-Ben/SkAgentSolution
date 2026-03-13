# Architecture
> Status: Updated after refactor
>
> 本文档描述当前代码实现对应的分层边界、运行时职责和主调用路径。
>
> Related:
> - `Docs/runtime.md`
> - `Docs/observability.md`
> - `Docs/skills.md`

## 1. 核心原则

### 1.1 SSOT：`AgentRunContext`
运行时事实统一写入 `SKAgent.Application.Runtime.AgentRunContext`：
- `Plan`：规划结果
- `Steps`：逐步执行事实
- `ToolCalls`：工具调用轨迹
- `ConversationState`：跨步骤共享状态（`profile`/`recent_turns`/`persona` 等）
- `FinalOutput`/`Status`：最终结果

### 1.2 Plan / Execute / Observe 分离
- 规划：`IPlanner`（当前实现 `PlannerAgent`）
- 执行：`PlanExecutor`
- 观测：`IRunEventSink` + `RunEvent`

### 1.3 Router 只做路由
`RouterAgent` 只根据 `AgentContext.Target` 分发到具体 `IAgent`，不负责重试策略和状态裁决。

## 2. 解决方案分层（当前）

- `SKAgent.Core`
  - 纯抽象与协议：`IAgent`、`IPlanner`、`IStepRouter`、`ITool*`、`RunEvent`、Plan/Step 模型
- `SKAgent.Application`
  - 应用编排与用例：`AgentRuntimeService`、`PlanExecutor`、Reflection、ToolInvoker、ChatContext
- `SKAgent.Agents`
  - 具体 Agent 能力：`SKChatAgent`、`McpAgent`、`RouterAgent`、`PlannerAgent`
- `SKAgent.Infrastructure`
  - 外部实现：`McpClient`、`InMemoryUserProfileStore`、`SseRunEventSink` 等
- `SKAgent.SemanticKernel`
  - Semantic Kernel 组装与插件
- `SKAgent.Host`
  - API 宿主、DI 组装、控制器入口

## 3. API 入口与运行模式

- 非流式：`POST /api/agent/run`
  - 控制器：`AgentController`
  - 返回：`AgentRunResponse`

- 流式（SSE）：`POST /api/agentstream/run`
  - 控制器：`AgentStreamController`
  - `SseRunEventSink` 注入 `RunAsync(..., eventSink: sink)`

两种模式复用同一套 `AgentRuntimeService` 与 `PlanExecutor`，区别仅在于是否注入事件 sink。

## 4. 运行时组件职责

- `AgentRuntimeService`
  - 创建 `AgentRunContext`
  - 加载 `IShortTermMemory` 与 `IUserProfileStore`
  - 调用 `IPlanner.CreatePlanAsync`
  - 调用 `PlanExecutor.ExecuteAsync`
  - 写回短期记忆与画像更新

- `PlannerAgent`
  - 读取 persona hint、profile、recent turns、tool catalog
  - 输出 `AgentPlan`

- `PlanExecutor`
  - 逐步执行 `PlanStep`
  - `kind=tool`：走 `IToolInvoker`
  - `kind=agent`：走 `IStepRouter` -> 目标 `IAgent`
  - 失败时触发 reflection/retry 决策

- `IRunEventSink`
  - 承接 `run_started`、`plan_created`、`step_*`、`tool_*`、`reflection_*`、`run_completed|run_failed`

## 5. 关键调用链

1. `AgentController` / `AgentStreamController` 接收请求
2. `AgentRuntimeService.RunAsync` 初始化上下文并发出 `run_started`
3. `IPlanner.CreatePlanAsync` 生成计划并发出 `plan_created`
4. `PlanExecutor.ExecuteAsync` 执行步骤并持续发出 step/tool/reflection 事件
5. 执行完成后发出 `run_completed`（失败则 `run_failed`）
6. Runtime 提交短期记忆与画像 patch

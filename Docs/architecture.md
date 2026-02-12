# Architecture
> Status: Draft (Week5)
>
> This doc defines the core architectural constraints: SSOT, separation of Plan/Execution/Result, and component boundaries.
>
> Related:
> - docs/runtime.md
> - docs/observability.md
> - ADR-0001 / ADR-0003

本项目目标：构建一个可工程化落地的 Agent Runtime，支持：
- 规划（Plan）与执行（Execution）解耦
- 多 Agent 路由、工具/技能（Tool/Skill）调用
- 可观测性（Trace / Run Event Model）
- 反思与重试（Reflection / Retry / Repair Plan）
- 同时支持非流式（稳定）与流式（事件流 SSE，Token 作为一种事件）

## 1. 核心原则（Non-negotiables）

### 1.1 Single Source of Truth（SSOT）
运行期间的“事实状态”必须写入 `RunContext`（或等价的运行时状态容器），而不是散落在局部变量、日志或隐式约定中。

- Plan 只是意图，不等于执行事实
- Execution 产生事实，并持续写入 RunContext
- Result 是对事实的最终汇总与投影（Response / Trace / SSE）

### 1.2 Plan / Execution / Result 分离
- Plan：由 Planner 生成步骤（Step），包含 expectedOutput 等约束
- Execution：由 Executor 逐步执行（AgentStep / ToolStep / RetrievalStep...）
- Result：聚合输出（finalOutput、stepOutputs、metrics、events）

### 1.3 Router 只负责路由，不负责状态
Router 做：
- 选择目标 Agent（基于上下文 state 或 Planner 的决策）

Router 不做：
- 不修改 RunContext 的核心状态
- 不承担重试、反思、工具选择等策略责任

## 2. 运行时主模块（Runtime Components）

- API Layer
  - 非流式：`POST /api/agent/run` -> 返回最终 `AgentRunResponse`
  - 流式：`POST /api/agent/run/stream`（SSE）-> 返回 Run 事件流

- RuntimeService
  - 协调一次 Run：初始化、规划、执行、收尾
  - 负责 run_started/run_completed/run_failed 事件

- Planner
  - 生成 Plan（steps）
  - 负责 plan_started/plan_created 事件

- Executor
  - 执行 steps，写入 RunContext（SSOT）
  - 负责 step_started/step_completed/step_failed 事件

- Tool System（Skills/Tools）
  - ToolRegistry：统一注册与发现
  - ToolInvoker：统一调用与错误封装
  - 负责 tool_invoked/tool_completed 事件

- Observability（Run Event Model）
  - RunEvent：统一事件模型（Trace & SSE 共用）
  - Exporters：Console / JSONL / SSE

- Reflection（反思/重试）
  - OutputEvaluator：判断 output 是否满足 expectedOutput
  - ReflectionAgent：给出修复策略（重试/改参/换工具/修计划）
  - PlanRepairer：对剩余步骤进行修补
  - 负责 reflection_triggered/retry_scheduled/plan_repaired 事件

## 3. 关键数据结构

- RunContext（SSOT）
  - runId、request、plan、stepStates、toolCalls、finalOutput、metrics
  - eventSeq（单 run 内递增序号）
  - 可选：eventSink（用于 emit RunEvent）

- Plan
  - steps[]：每个 Step 具有 id/kind/name/inputs/expectedOutput 等

- RunEvent（统一事件）
  - envelope：runId, ts, seq, type, payload
  - payload 随 type 变化（见 docs/observability.md）

## 4. 工程化验收（High-level Acceptance）

- 非流式接口稳定返回最终结果（不依赖 SSE）
- SSE 能稳定输出：
  - run_started -> plan_created -> step_* -> run_completed（或 run_failed）
  - 工具调用阶段无 token 也能发进度事件
  - chat token 以事件形式插入（可选）
- 所有关键策略（工具协议、事件 schema、重试策略）有 ADR 文档留痕

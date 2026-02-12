# Runtime
> Status: Draft (Week5)
>
> Defines the lifecycle of a run and how Non-stream and SSE stream share the same runtime.
>
> Related:
> - docs/architecture.md
> - docs/observability.md
> - docs/diagrams/runtime-sequence.mmd

本文件描述一次 Agent Run 的生命周期、组件责任、以及“非流式与 SSE 流式”如何复用同一套运行时。

## 1. Run 生命周期（概览）

一次 run 通常包含：
1) run_started
2) plan_started -> plan_created
3) step 循环执行（可能包含 tool、retrieval、chat、reflection）
4) 汇总输出（finalOutput）
5) run_completed / run_failed

## 2. 运行时序（责任边界）

### 2.1 RuntimeService（Orchestrator）
- 初始化 RunContext（runId、eventSeq=0）
- 调用 Planner 生成 plan
- 调用 Executor 执行 plan
- 聚合结果并返回（非流式）
- 或把过程事件写入 SSE（流式）

必须发事件：
- run_started
- run_completed / run_failed

### 2.2 Planner（Plan Producer）
- 输入：RunContext（含用户输入、历史对话、可用 tools/agents）
- 输出：Plan（steps）
- 可附 expectedOutput / constraints
- 不直接执行工具或 agent（避免与 Executor 责任混淆）

必须发事件：
- plan_started
- plan_created（建议只发摘要，避免泄露 prompt/tool secrets）

### 2.3 Executor（Fact Maker）
- 逐 step 执行，并将事实写入 RunContext
- step kind 可能包括：
  - agent_step：调用子 agent / 模型对话
  - tool_step：调用 ToolInvoker
  - retrieval_step：RAG/检索
  - reflection_step：触发反思与计划修复（可选）

必须发事件：
- step_started / step_completed / step_failed

## 3. 非流式与流式（SSE）并存

### 3.1 非流式（默认稳定）
- API：`POST /api/agent/run`
- 返回：最终 AgentRunResponse
- 适用：弱网、批处理、脚本测试、回放

### 3.2 流式（事件流 SSE）
- API：`POST /api/agent/run/stream`
- Response：`text/event-stream`
- SSE 事件序列：
  - run_started
  - plan_created
  - step_started/step_completed（每步）
  - tool_invoked/tool_completed（若有）
  - chat_delta（可选）
  - run_completed（含 finalOutput）

### 3.3 “同一套 RuntimeService/Executor 复用”的实现策略
- RuntimeService/Planner/Executor 内部只依赖 `IRunEventSink`（可选）
- 非流式：注入 NullSink（不输出事件）
- SSE：注入 SseSink（把 RunEvent 写入 Response）

## 4. RunContext（SSOT）建议字段

- Identity：runId, requestId, sessionId
- Input：userInput, attachments, metadata
- Plan：plan, plannerModel, plannerPromptHash
- Steps：stepStates（status/start/end/error/outputPreview）
- Tools：toolCalls（invocations/results/latency）
- Metrics：tokens, latency, retries, errorCounts
- Events：eventSeq（用于事件排序）
- Final：finalOutput, summary

## 5. 错误与取消（Cancellation）

- 任意阶段发生异常：
  - 写入 RunContext.error
  - emit run_failed（含 stepId 若可定位）
- 支持 CancellationToken：
  - SSE 下需尽快 flush 终止事件（可发送 run_failed/cancelled）

# Observability (Run Event Model + Trace + SSE)
> Status: Draft (Week5-2)
>
> Run Event Model is the single observability schema. Trace + SSE are exporters.
>
> Related:
> - ADR-0003
> - docs/runtime.md

本项目的可观测性以 **Run Event Model** 为中心：
- 运行时所有重要事实以 RunEvent 形式 emit
- Trace、日志、SSE、指标都是对 RunEvent 的不同投影（Exporters）

## 1. 为什么是事件流而不是纯 token 流

Agent run 往往包含：
- plan 生成
- 多步执行（工具、外部 API、检索、反思）
- 汇总输出

其中大量阶段不会产生 token，但用户与开发者都需要“进度感”和“定位能力”。
因此：
- SSE 以事件流为主
- token 流（chat_delta）作为事件类型之一（可选）

## 2. RunEvent Envelope（统一结构）

建议所有事件使用统一 envelope：

- runId: string
- ts: string (ISO8601 UTC)
- seq: long (单 run 内递增)
- type: string (事件类型)
- payload: object (随 type 变化)

seq 用于前端排序/去重，也用于 trace 回放稳定性。

## 3. 事件类型（最小集）

### Run
- run_started
  - payload: requestSummary, modelInfo?
- run_completed
  - payload: finalOutput, summary, metrics
- run_failed
  - payload: error(code/message), failedStepId?, metrics

### Plan
- plan_started
  - payload: plannerInfo?
- plan_created
  - payload: stepCount, stepsSummary[]（避免泄露敏感内容）

### Step
- step_started
  - payload: stepId, kind, name
- step_completed
  - payload: stepId, success, outputPreview, latencyMs
- step_failed
  - payload: stepId, error(code/message), latencyMs

### Tool
- tool_invoked
  - payload: stepId, toolName, argsPreview
- tool_completed
  - payload: stepId, toolName, success, latencyMs, outputPreview

### Chat (Optional)
- chat_delta
  - payload: stepId, deltaText
- chat_message_completed
  - payload: stepId, messagePreview, tokens?

### Reflection
- reflection_triggered
  - payload: stepId, reason(step_failed|output_mismatch)
- retry_scheduled
  - payload: stepId, attempt, maxAttempts
- plan_repaired
  - payload: repairedFromStepIndex, newStepsSummary

## 4. Exporters（投影）

- ConsoleExporter
  - 开发期输出摘要：Run Summary / Step Timeline
- JsonlExporter
  - 落盘：每行一个 RunEvent（便于回放与分析）
- SseExporter（核心体验）
  - `text/event-stream`
  - 推荐格式：
    - event: {type}
    - data: {json(envelope)}

## 5. SSE 前端渲染建议（最小）

- 以 step_started/step_completed 构建进度条与步骤列表
- 工具调用显示 tool_invoked/tool_completed
- chat_delta 追加到当前 step 的输出窗口
- run_failed 标红并定位 failedStepId

## 6. Week5-2 验收标准

- RuntimeService/Planner/Executor/ToolInvoker 至少能 emit：
  - run_started, plan_created, step_started, step_completed, tool_invoked, tool_completed, run_completed
- SSE endpoint 实际可用（浏览器/前端可接收并渲染）
- 非流式接口不受影响（NullSink）

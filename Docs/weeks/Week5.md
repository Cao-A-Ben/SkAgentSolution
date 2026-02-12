# Week5 - Skills/Tools + Run Event Model(SSE/Trace) + Reflection
> Status: In Progress
>
> Week5 execution plan and acceptance criteria (Tools + Run Event Model/SSE + Reflection).

Week5 目标：工程化闭环。核心交付是 “工具协议 + 事件模型 + 反思重试”，并保证非流式接口稳定不受影响。

---

## 0. 总验收（Week5 Done Definition）

- [ ] 非流式 `POST /api/agent/run` 行为不变、稳定返回最终 AgentRunResponse
- [ ] Tool/Skill 有统一注册与调用协议，Planner 能生成 ToolStep
- [ ] Run Event Model 落地：运行过程能 emit 标准事件
- [ ] SSE Endpoint 可用：前端能看到规划/执行步骤/工具调用/最终完成
- [ ] Reflection 最小闭环：失败或不满足 expectedOutput 可触发有限次重试

---

## 1. Week5-1：Skills / Tools 标准化

### Scope
- ToolDescriptor/Invocation/Result/Registry/Invoker
- 至少一种 Tool Adapter（HTTP 或本地函数）
- Planner 能产出 ToolStep
- Executor 能执行 ToolStep 并写入 RunContext

### Acceptance
- [ ] ToolRegistry 能列出可用 tools（供 Planner 使用）
- [ ] ToolStep 执行过程中产生 tool_invoked/tool_completed 事件
- [ ] Tool 调用异常 -> ToolResult.error（不直接抛到上层，除非不可恢复）

---

## 2. Week5-2：Run Event Model + Exporters（Trace/SSE 共用）

### Scope
- RunEvent 统一 envelope（runId/ts/seq/type/payload）
- IRunEventSink（可选注入；默认 NullSink）
- Exporters：
  - Console（开发期）
  - JSONL（可选）
  - SSE（必须）

### SSE 事件序列（最小）
- run_started
- plan_created
- step_started / step_completed（每步）
- tool_invoked / tool_completed（若有）
- run_completed（含 finalOutput）

### Acceptance
- [ ] Executor/Planner/RuntimeService/ToolInvoker 至少能 emit 上述事件
- [ ] SSE endpoint 可被浏览器/EventSource 或 fetch stream 消费
- [ ] Non-stream run 仍然 OK（NullSink）

---

## 3. Week5-3：Reflection（反思/重试/修计划）

### Scope
- OutputEvaluator（简单规则）
- RetryPolicy（限制次数，避免无限循环）
- ReflectionAgent（给出 retry/repair action）
- 事件：
  - reflection_triggered
  - retry_scheduled
  - plan_repaired（可选但推荐）

### Acceptance
- [ ] Step 失败触发反思与有限重试
- [ ] Output mismatch 触发反思（最小规则即可）
- [ ] 达到上限后明确 run_failed，并定位 failedStepId

---

## 4. 目录结构变更（对齐 docs/skills & docs/observability & docs/reflection）

建议新增：
- Src/SkAgent/Tools/*
- Src/SkAgent/Observability/*
- Src/SkAgent/Reflection/*

---

## 5. 风险与约束

- SSE 增强体验但会提高前端复杂度；后端需保证：
  - flush 及时
  - 取消与异常能正确关闭流并发送 run_failed
- 事件 payload 注意脱敏：
  - tool args 只发 preview
  - 不发完整 prompt / secret

---

## 6. 输出物清单

- docs：
  - skills.md / observability.md / reflection.md（规范）
  - ADR 0002/0003/0004（关键决策留痕）
  - diagrams 更新（时序与状态流）
- code：
  - Tool 协议、RunEvent、SSE endpoint、Reflection 最小闭环
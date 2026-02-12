# ADR-0001: Single Source of Truth via RunContext

## Status
Accepted

## Context
Agent 系统包含规划、执行、工具调用与反思重试。若运行状态分散在局部变量、日志、临时对象中，会导致：
- 上下文漂移（对话描述与真实代码状态不一致）
- 难以调试（无法定位失败 step）
- 难以回放（缺乏可重放的事实序列）

## Decision
引入 RunContext 作为 SSOT：
- 所有执行产生的事实（step outputs/tool results/metrics）必须写入 RunContext
- Planner 输出的 Plan 作为意图保存，但不得替代执行事实
- Result（Response/Trace/SSE）作为 RunContext 的投影

## Consequences
Positive:
- 工程可调试、可回放、可观测
- 反思/重试更容易实现（基于事实判断）
Negative:
- RunContext 结构需要设计与治理（避免膨胀）
- 需要明确字段脱敏策略

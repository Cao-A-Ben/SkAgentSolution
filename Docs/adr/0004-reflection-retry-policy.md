# ADR-0004: Reflection + Retry Policy with Hard Limits

## Status
Accepted

## Context
Agent 执行可能失败或输出不满足 expectedOutput。若无反思重试，成功率低；若无上限，可能无限循环并消耗资源。

## Decision
引入 Reflection 闭环，并使用强约束 RetryPolicy：
- 触发条件：step_failed 或 output_mismatch
- 动作：retry same step / retry with modified args / swap tool / repair remaining plan
- 上限：
  - maxRetriesPerStep
  - maxReplansPerRun
- 所有反思与重试必须 emit 事件：reflection_triggered、retry_scheduled、plan_repaired

## Consequences
Positive:
- 成功率提升，可控可解释
Negative:
- 策略设计需要迭代（从简单规则到更智能评估）

# Reflection (反思 / 重试 / 修计划)
> Status: Draft (Week5-3)
>
> Defines triggers, retry limits, and plan repair rules.
>
> Related:
> - ADR-0004
> - docs/observability.md (reflection events)

目标：当 step 失败或 output 不满足 expectedOutput 时，触发反思并执行有限次重试或修复计划，使系统具备“自我纠错”的工程闭环。

## 1. 触发条件（Triggers）

- Step Failure Trigger
  - step 执行异常、ToolResult.success=false、超时
- Output Mismatch Trigger
  - step output 与 expectedOutput 不匹配
  - 最小实现：规则匹配/关键词/结构校验
  - 后续可升级：LLM evaluator / schema validator

触发时必须 emit：
- reflection_triggered（reason=step_failed 或 output_mismatch）

## 2. 策略输出（Reflection Outcome）

反思可能给出以下动作之一：

1) Retry Same Step
- 适用：临时性错误、外部服务抖动、模型偶发
- 必须受 RetryPolicy 限制（attempt <= max）

2) Retry With Modified Arguments
- 适用：参数不对、prompt 不对、查询词不对

3) Swap Tool / Fallback Tool
- 适用：某工具不可用或返回质量差
- 需要工具具备 tags/capabilities 方便 planner/repairer 替换

4) Repair Remaining Plan (Replan)
- 适用：原计划不成立，需改变后续步骤
- 约束：不得改写已完成步骤事实，只能修补“未执行部分”

## 3. RetryPolicy（限制重试，避免无限循环）

建议参数：
- maxRetriesPerStep（例如 2）
- maxReplansPerRun（例如 1）
- backoffMs（可选）
- retryOnToolTimeout: bool
- retryOnModelError: bool

触发重试时 emit：
- retry_scheduled（attempt/maxAttempts）

修计划成功 emit：
- plan_repaired（摘要：从哪一步开始修补、新 steps 数量）

## 4. 执行路径（简化）

Executor 在 step 完成后：
1) 若失败 -> ReflectionAgent -> 根据策略 retry/repair
2) 若成功但 output mismatch -> ReflectionAgent -> retry/repair
3) 若达到上限 -> step_failed 或 run_failed

## 5. Week5-3 验收标准

- 至少实现：
  - OutputEvaluator（简单规则）
  - RetryPolicy（上限）
  - ReflectionAgent（返回 action）
- 事件流可看到 reflection_triggered、retry_scheduled、plan_repaired（至少前两者）
- RunContext 中记录：retries、repairHistory

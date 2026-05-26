# Repair Plan

- Status: Week11 Accepted
- Owner: Ben + Codex
- Last Updated: 2026-05-15
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](./observability-replay.md)
  - [Week11 Acceptance Runbook](./week11-acceptance-runbook.md)
  - [Week11 Acceptance Result](./week11-acceptance-result.md)

## 文档目的

这份文档固定 Week11 的唯一目标：把已有的 `Reflection + retry` 升级为**可解释的 repair plan 机制**，并继续复用现有 replay / observability / Replay UI，而不是另起一套失败回放体系。

## 当前实现范围

- 已新增 `FailureSource`：
  - `planner`
  - `executor`
  - `tool`
  - `memory`
- 已新增 `IReviewer` 抽象。
- 当前默认 reviewer 为规则型实现，不依赖新的 LLM reviewer。
- 已保留现有 `ReflectionAgent / retry_scheduled` 路径，确保 Week10 之前的重试语义不被破坏。

## 当前 repair 事件链

- `repair_plan_created`
- `repair_step_started`
- `repair_step_completed`

说明：

- `repair_plan_created` 是 repair 摘要的事实源。
- `repair_step_*` 当前记录的是 reviewer 在当前 run 中发布 repair 建议步骤时的真实状态变化。
- replay detail 会以 `repair_plan_created` 为基线，并继续叠加 `repair_step_started / repair_step_completed` 的后续状态。
- Week11 默认**不会自动改写原 plan 并继续执行**；repair plan 先以“解释和建议”为主。

## 当前默认分流策略

### tool

- 工具超时、网络抖动、限流等失败统一先归到 `tool`
- 若错误可重试，则仍保留原有 `retry_scheduled`
- 若达到重试上限或被判定为不可恢复，则生成 repair plan

### executor

- Agent step 返回失败、或执行阶段抛出非 tool 异常时归到 `executor`
- repair plan 默认建议先检查失败 step 的输出与状态，再决定 rerun 或替换步骤

### planner

- planner prompt 生成或 planner output 解析失败时归到 `planner`
- repair plan 默认建议检查 planner prompt、模型输出与 JSON 契约，然后重建 plan

### memory

- RunPreparation / memory bundle 生成失败时归到 `memory`
- repair plan 默认建议检查 retrieval routes、memory inputs，并在必要时回退到 recent history

## Replay 与 UI

- repair 继续写入同一条 JSONL event timeline
- replay detail 现在会额外投影 `repair`
- Replay UI 的 run detail 现已补充：
  - repair timeline filter
  - repair panel
  - failure source / category / reason / recommended steps
  - repair step 状态摘要（planned / running / completed / failed / skipped）

## 当前边界

- 已完成实现：
  - reviewer 抽象
  - repair plan 模型
  - tool/executor/planner/memory 的基础失败分流
  - repair 事件链
  - replay 投影
  - Replay UI repair 面板
  - 四类 failure source 的真实测试样例
  - repair step 实时状态投影与 UI 状态摘要
- 暂未完成：
  - 自动执行 repair step
  - plan diff 驱动的 remaining plan 修补
  - 更细的 provider-specific repair 策略

## 当前决策

- Week11 **不进入**“部分自动 repair 执行”阶段。
- 当前固定决策是：
  - repair plan 继续保持 `recorded / review-first / manual-only`
  - 先补齐四类失败来源的真实样例与 replay 展示
  - 先把 demo 能力做成“失败可解释、修复建议可讲解”，而不是让 runtime 在 Week11 内直接改 plan 继续跑

原因：

- 当前最需要收口的是 failure taxonomy、event schema 和 replay/demo 讲解路径。
- 如果现在就进入自动 repair，会同时引入：
  - plan 被动态改写后的验收复杂度
  - 工具副作用与重复执行风险
  - 更多状态兼容成本
- 这些更适合放到 Week12 之后，在 MCP / Demo 主线稳定后单独推进。

## 下一步

1. Week12 继续把 repair 结果纳入 demo 路线与讲解脚本。
2. 在 MCP / skill 接入后，继续复用当前 failure taxonomy 与 replay 结构。
3. 在 Week12 之后，再单独评估“部分 repair 自动执行”的边界与风险。

# Week11 Acceptance Runbook

- Status: Accepted
- Owner: Ben + Codex
- Last Updated: 2026-05-18
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [Repair Plan](./repair-plan.md)
  - [Observability & Replay](./observability-replay.md)
  - [Replay UI](./replay-ui.md)
  - [Week11 Acceptance Result](./week11-acceptance-result.md)

## 文档目的

这份 runbook 用于 Week11 的正式验收。它把“失败样例准备”“事件链核对”“Replay UI 核对”和“验收结论记录”放在一起，确保 Week11 的 `Reviewer / Repair Plan` 不只是代码完成，而是真正经过可复现验证。

当前目标不是验证“自动修复”，而是验证：

- 失败来源分流是否稳定
- repair 事件链是否完整
- replay/detail/UI 是否能解释失败与修复建议
- 文档、事件与页面展示是否一致

## Week11 验收范围

### 必须通过

- `planner / executor / tool / memory` 四类失败来源，至少各有一条可复现样例。
- 每条失败 run 都能生成 repair plan。
- 每条失败 run 的事件日志中都能看到：
  - `repair_plan_created`
  - `repair_step_started`
  - `repair_step_completed`
- Replay UI 的 run detail 能展示：
  - failure source
  - failure category
  - failure phase
  - repair reason
  - repair steps
  - repair step status

### 明确不在 Week11 验收内

- 自动执行 repair step
- 动态改写 remaining plan 并继续执行
- MCP / skill 正式接入
- 最终 demo 录屏与 Week12 runbook 收口

## 当前实现基线

### 已确认对齐

- `IReviewer` 已接入 runtime。
- `FailureSource` 已固定为：
  - `planner`
  - `executor`
  - `tool`
  - `memory`
- repair 事件链已固定为：
  - `repair_plan_created`
  - `repair_step_started`
  - `repair_step_completed`
- replay detail 当前会把 `repair_plan_created` 与后续 `repair_step_*` 状态合并投影。
- Replay UI 当前已提供：
  - repair filter
  - repair panel
  - repair step 状态摘要

### 当前本地入口

- Host API:
  - `http://localhost:5192`
  - `https://localhost:7108`
- Replay UI:
  - `http://localhost:4179`

## 第 0 步：执行前硬检查

在跑验收前，先确认以下事项。

### 代码与测试检查

- `dotnet test Tests/SKAgent.Tests/SKAgent.Tests.csproj` 已通过。
- `Frontend/SKAgent.ReplayApp` 下的 `npm run build` 已通过。

### 文档检查

以下文档应已更新为 Week11 “已验收通过” 口径：

- [README.md](../../README.md)
- [Product Journey](../01-roadmap/product-journey.md)
- [System Overview](../02-architecture/system-overview.md)
- [Observability & Replay](./observability-replay.md)
- [Repair Plan](./repair-plan.md)

### 验收边界检查

- 当前验收只确认“可解释 repair”链路，不应把“自动 repair 未实现”误判为失败。

## 第 1 步：启动 Host 与 Replay UI

### 启动 Host

```bash
dotnet run --project Src/SKAgent.Host/SKAgent.Host.csproj
```

### 启动 Replay UI

```bash
cd Frontend/SKAgent.ReplayApp
npm install
npm run dev
```

## 第 2 步：准备四类失败样例

建议统一使用带前缀的 `conversationId`，避免和其他 replay 混淆。

推荐：

- `week11-tool-001`
- `week11-executor-001`
- `week11-planner-001`
- `week11-memory-001`

### A. tool 失败样例

目标：触发 tool timeout、tool bad request 或 tool not found 中至少一个。

验收重点：

- `failureSource = tool`
- repair steps 中包含针对 tool 的具体建议
- 若是 timeout / rate limit，建议包含 backoff / runtime health 相关动作

### B. executor 失败样例

目标：触发 agent step 返回失败，或执行阶段出现非 tool 异常。

验收重点：

- `failureSource = executor`
- repair steps 中包含：
  - 检查失败 step 输出/状态
  - 冻结后续步骤并从失败点重规划

### C. planner 失败样例

目标：触发 planner prompt / plan 生成异常，或 planner output 解析异常。

验收重点：

- `failureSource = planner`
- repair steps 中包含：
  - 校验 planner prompt / JSON contract
  - 从简化输入重建 plan

### D. memory 失败样例

目标：触发 `RunPreparation` / memory bundle 生成失败。

验收重点：

- `failureSource = memory`
- repair steps 中包含：
  - 检查 retrieval routes / memory inputs
  - fallback 到 recent history only

## 第 3 步：核对事件链

对每条失败 run，打开对应 JSONL 或 `/api/replay/runs/{runId}/events` 结果，确认至少包含：

- `run_started`
- 失败前的关键上下文事件
- `repair_plan_created`
- 一组 `repair_step_started`
- 一组 `repair_step_completed`
- `run_failed`

### 必须核对的 payload 字段

#### `repair_plan_created`

- `failureSource`
- `failureCategory`
- `reason`
- `failedPhase`
- `failedOrder`
- `repairStepCount`
- `repairSteps[]`

#### `repair_step_started / repair_step_completed`

- `repairStepId`
- `title`
- `action`
- `target`
- `status`
- `notes`

## 第 4 步：核对 Replay UI

打开对应 run detail 页面，重点看以下区域。

### Timeline

- 能筛选 `Repair`
- `repair_plan_created`
- `repair_step_started`
- `repair_step_completed`
- `run_failed`

### Repair Panel

- 显示 failure source
- 显示 failure category
- 显示 failure phase
- 显示 repair reason
- 显示 repair step 列表
- 每个 step 能看到 action / target / status

### Rail / Summary

- `Repair steps` 统计正常
- repair 状态摘要正常
- 不会把 `planned / running / failed / skipped` 全部显示成同一种状态

## 第 5 步：验收判定标准

### 通过标准

当以下条件全部成立时，可判定 Week11 通过：

1. 四类 failure source 都至少有一条可复现样例。
2. 四类样例都能稳定产出 repair plan。
3. 四类样例都能在 replay 事件链中看到 repair 事件。
4. Replay UI 能稳定展示 repair 来源、原因、步骤和状态。
5. 文档口径与页面表现一致。

### 不通过的典型情况

- 失败 run 只出现 `run_failed`，没有 `repair_plan_created`
- `failureSource` 归因错误
- repair steps 只有泛化文案，没有区分 failure type
- Replay UI 只显示静态 planned，没有反映 `repair_step_*` 的后续状态
- 文档仍写着“进行中”或与真实行为不一致

## 第 6 步：建议保存的验收证据

建议至少保留以下材料：

- 4 条失败 run 的 `runId`
- 每条 run 的 Replay UI 截图
- 每条 run 的关键事件片段
- 一份最终验收结论

建议截图位点：

- run list 中能看到失败 run
- run detail 的 timeline repair filter
- run detail 的 repair panel
- 至少一条 run 的 raw payload 展开图

## 验收结论模板

可以直接按下面模板记录：

```md
# Week11 Acceptance Result

- Date:
- Operator:
- Host URL:
- Replay UI URL:

## Sample Runs

- tool:
- executor:
- planner:
- memory:

## Result

- [ ] 四类 failure source 样例均已完成
- [ ] repair_plan_created 均已生成
- [ ] repair_step_started / completed 均可见
- [ ] Replay UI repair panel 正常
- [ ] 文档口径一致

## Notes

- 
```

## 当前建议

- 若你准备立即进入 Week12，建议先完成这份 runbook 的一次人工验收，并把 4 条样例 runId 固定下来。
- 一旦这 4 条 runId 固定，Week12 的 demo 讲解可以直接复用，不需要重新造失败样本。

# Current Status

- Status: Working Summary
- Owner: Ben + Codex
- Last Updated: 2026-05-26
- Related:
  - [README](../../README.md)
  - [Product Journey](./product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](../03-modules/observability-replay.md)
  - [Repair Plan](../03-modules/repair-plan.md)
  - [Week11 Acceptance Runbook](../03-modules/week11-acceptance-runbook.md)
  - [Demo Runbook](../03-modules/demo-runbook.md)

## Current Phase

项目当前已经离开 Week11 的开发阶段，进入 Week12 的实现收口与验收整理阶段。

当前建议口径：

- Week11: Accepted
- Week12: Implemented in core path, pending final acceptance closure
- Overall: In acceptance and demo closure phase

## Completed Milestones

- Week8 foundation completed
- Week8.x persona switching / coach completed
- Week8.5 model routing + rerank + progress recall completed
- Week9 standalone Replay UI completed
- Week10 voice conversation MVP completed
- Week11 reviewer / repair plan implementation completed
- Week12 external tool governance completed
- Week12 minimal skill runtime completed
- Week12 demo runbook and sample capture scripts completed

## Week11 Implemented Scope

Week11 fixed goals are already implemented:

- `Reviewer / RepairPlan` abstraction
- repair event chain:
  - `repair_plan_created`
  - `repair_step_started`
  - `repair_step_completed`
- failure source routing:
  - `planner`
  - `executor`
  - `tool`
  - `memory`
- replay detail repair projection
- Replay UI repair panel and repair filter
- failure sample and projection tests

Week11 intentionally did not include:

- automatic repair step execution
- remaining plan rewrite and resume
- MCP / skill formal integration

## Week12 Implemented Scope

Week12 core implementation has already started and the main demo path is in place:

- external tool governance:
  - planner-visible external tools
  - execution allowlist
  - audit events for external calls
- minimal skill runtime:
  - `GET /api/skills`
  - `skill_selected`
  - demo skill `tech.mcp_demo`
  - planner hint + replay projection
- Week12 demo assets:
  - [Demo Runbook](../03-modules/demo-runbook.md)
  - `Scripts/week12/capture-demo-samples.sh`
  - `Scripts/week12/capture-demo-samples.ps1`
  - `Scripts/week12/capture-blocked-tool-drill.sh`
  - `Scripts/week12/capture-blocked-tool-drill.ps1`
- captured demo evidence under:
  - `artifacts/week12-demo/`
  - `artifacts/week12-demo-blocked/`

## Remaining Work

### Week11

Week11 is already accepted. The remaining work is no longer implementation or acceptance, but reuse and archival:

- keep the accepted wording aligned across SSOT docs
- reuse the accepted evidence in Week12 demo and closure materials

### Week12

Week12 is functionally close, but still needs final closure work:

- confirm one reproducible end-to-end demo route
- verify demo evidence bundle is the final preferred set
- decide whether to keep the current demo skill as the official Week12 sample or replace it with a stronger domain sample
- complete final acceptance wording in SSOT docs

## Current Risks / Debt

- `Microsoft.SemanticKernel.Core` `1.70.0` currently raises a known vulnerability warning during test/build
- there are still a few compiler/code-analysis warnings:
  - nullable warnings in infrastructure and agents
  - `CA2024` warning in `ReplayQueryService`

These do not block the current Week11 / Week12 acceptance path, but should be tracked before broader external rollout.

## Recommended Next Step

如果目标是尽快收口，最合理的顺序是：

1. 正式执行 Week11 acceptance runbook，并把结论固定下来。
2. 复用现有 Week12 demo evidence，跑一次最终 demo walkthrough。
3. 再决定是否把 Week12 标记为 completed，或追加一个更强的真实 MCP / skill 示例。

## Verification Snapshot

As of 2026-05-26 local verification:

- `dotnet test Tests/SKAgent.Tests/SKAgent.Tests.csproj --no-restore`
  - Passed: 50
  - Skipped: 1
  - Failed: 0
- `Frontend/SKAgent.ReplayApp`
  - `npm run build` passed

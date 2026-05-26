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
  - [Week12 Acceptance Runbook](../03-modules/week12-acceptance-runbook.md)
  - [Week12 Acceptance Result](../03-modules/week12-acceptance-result.md)

## Current Phase

项目当前已经离开 Week11 的开发阶段，进入 Week12 的实现收口与验收整理阶段。

当前建议口径：

- Week11: Accepted
- Week12: Accepted
- Overall: In final closure phase

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

Week12 core implementation is complete and the main demo path is accepted:

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

Week12 is accepted. Remaining work is no longer Week12 implementation, but post-acceptance closure:

- keep accepted wording aligned across SSOT docs
- reuse the canonical demo bundles for any final recording refresh
- treat future dependency upgrades as routine maintenance instead of Week12 scope

## Current Risks / Debt

- no active build-time warnings remain in the current baseline
- future package upgrades and broader regression checks should continue as routine maintenance

## Recommended Next Step

如果目标是尽快收口，最合理的顺序是：

1. Reuse the accepted Week12 demo bundles for any final walkthrough or recording refresh.
2. Move remaining work into closure/debt buckets instead of continuing Week12 scope creep.
3. If needed, plan stronger real MCP adapters or richer domain skills as post-Week12 work.

## Verification Snapshot

As of 2026-05-26 local verification:

- `dotnet test Tests/SKAgent.Tests/SKAgent.Tests.csproj --no-restore`
  - Passed: 50
  - Skipped: 1
  - Failed: 0
- `Frontend/SKAgent.ReplayApp`
  - `npm run build` passed

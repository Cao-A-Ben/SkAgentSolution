# Week12 Acceptance Runbook

- Status: Accepted
- Owner: Ben + Codex
- Last Updated: 2026-05-26
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [Tools & MCP](./tools-mcp.md)
  - [Demo Runbook](./demo-runbook.md)
  - [Observability & Replay](./observability-replay.md)
  - [Week12 Acceptance Result](./week12-acceptance-result.md)

## Purpose

This runbook fixes the Week12 acceptance scope and the minimum evidence needed to close the MCP / Skills / Demo phase.

Week12 is accepted when we can show:

- external tools enter one unified tool governance path
- skill selection stays inside the same runtime and replay system
- the demo route is reproducible end to end
- demo evidence is already archived in the repo

## Acceptance Scope

### Must pass

- external tool governance exists and is verified:
  - planner-visible filtering
  - execution allowlist
  - audit events
- skill runtime exists and is verified:
  - `GET /api/skills`
  - `skill_selected`
  - planner hint composition
  - replay skill projection
- demo route is fixed for:
  - text chat
  - replay
  - daily suggestion
  - voice
  - MCP / skill example
- evidence is archived and reusable

### Explicitly out of scope

- production MCP vendor integration
- a marketplace-grade skill catalog
- automatic repair execution
- large frontend redesign

## Canonical Evidence

### Verification

- `dotnet test Tests/SKAgent.Tests/SKAgent.Tests.csproj --no-restore`
- `cd Frontend/SKAgent.ReplayApp && npm run build`

### Real demo bundles

- main demo bundle:
  - [artifacts/week12-demo/20260521-160918/summary.md](../../artifacts/week12-demo/20260521-160918/summary.md)
- blocked-tool drill:
  - [artifacts/week12-demo-blocked/20260521-161054/summary.md](../../artifacts/week12-demo-blocked/20260521-161054/summary.md)

### Test evidence

- tool governance:
  - [ToolGovernanceTests](../../Tests/SKAgent.Tests/Tools/ToolGovernanceTests.cs)
- skill runtime:
  - [SkillRuntimeTests](../../Tests/SKAgent.Tests/Skills/SkillRuntimeTests.cs)
- replay skill projection:
  - [ReplaySkillProjectionTests](../../Tests/SKAgent.Tests/Replay/ReplaySkillProjectionTests.cs)

## Official Week12 Sample Decision

Week12 accepts the current demo pair as the official sample:

- external tool: `mcp.demo_echo`
- skill: `tech.mcp_demo`

Stronger production-oriented domain skills or richer MCP adapters are moved to post-Week12 scope.

## Acceptance Checklist

- [x] `mcp.demo_echo` is governed by planner visibility and execution allowlist rules
- [x] `external_call_started / external_call_finished / external_call_blocked` are covered by tests and demo evidence
- [x] `GET /api/skills` is available
- [x] `tech.mcp_demo` is discoverable and usable as the official Week12 sample
- [x] `skill_selected` enters the same replay timeline
- [x] replay detail projects `skill` summary
- [x] Week12 demo route is documented in one SSOT runbook
- [x] main demo bundle is archived
- [x] blocked-tool drill bundle is archived
- [x] Week11 repair evidence is reusable inside Week12 drill/demo

## Result

Week12 is accepted.

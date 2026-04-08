# Tools & MCP

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-03-31
- Related:
  - [System Overview](../02-architecture/system-overview.md)
  - [Runtime Lifecycle](../02-architecture/runtime-lifecycle.md)

## 文档目的

这份文档说明 Tool 协议、未来 MCP 接入方式，以及外部调用为什么必须和 Runtime / Observability 保持一致。

## Tool 的定位

Tool 是 Runtime 可以调度的外部能力单元。它们可能是：

- 本地纯函数型工具
- 基础设施工具
- 外部系统连接器
- 未来的 MCP adapter
- 领域 Skills 背后的执行工具

## 当前原则

- Tool 是 Planner / Executor 可见的动作能力。
- Tool 的输入输出应可记录、可脱敏、可回放。
- 不允许把外部调用做成 Runtime 之外的黑盒旁路。

## MCP 的接入方式

### 设计方向

- MCP 将作为 Tool adapter 进入系统，而不是单独发明第二套协议。
- Runtime 仍通过统一 Tool 协议调度它。
- Allowlist、权限、审计和脱敏统一复用现有事件体系。

### Week11 目标

- 增加 `ExternalCallPolicy`。
- 增加 `McpToolAdapter`。
- 接入至少一个外部系统。
- 记录 `external_call_started`、`external_call_finished`、`external_call_blocked`。

## 领域 Skills 的位置

- Skills 不是另一套 Runtime，它们是产品级能力包。
- 领域 Skills 通过 Prompt 模板、Tool 组合和规则策略参与运行时。
- 中医 / 技术 Skills 将在 Week12 收敛为可演示能力。

## 当前状态

- Tool 基础协议已存在。
- MCP 仍处于规划阶段。
- 文档体系已经为后续 Week11 / Week12 预留位置。

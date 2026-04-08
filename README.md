# SKAgentSolution

产品级 Agent Runtime 项目，Week7 已完成并通过真实环境验收，当前进入 Week8：Daily Suggestion Job。

## 当前定位

- 我们正在把项目从“能跑的 Agent Demo”推进到“可展示、可复现、可审计的产品级 Runtime”。
- Week7 已完成：Intent Router、Memory Fusion、pgvector、Recent Recall 和事件链路已通过验收。
- 当前优先级是启动 Week8：Daily Suggestion Job、Suggestion Store 和回放指针。
- 关于为什么继续当前项目、它与 agent / claw-like 产品的关系、以及未来如何扩展，可参见 [Product Positioning](Docs/01-roadmap/product-positioning.md) 文档。

## 快速开始

1. 安装 .NET SDK（`net10.0`）。
2. 配置 `Src/SKAgent.Host/appsettings*.json`（至少 OpenAI；后续建议配置 `ConnectionStrings:PgVector`）。
3. 先阅读文档主线，确认当前阶段和结构。
4. 如需继续推进，请先看 Week7 验收结果，再进入 Week8 的 Daily Suggestion 设计与实现。

运行 Host：

```bash
dotnet run --project Src/SKAgent.Host/SKAgent.Host.csproj
```

## 主文档导航

### 项目进度

- [Product Journey](Docs/01-roadmap/product-journey.md)
- [Product Positioning](Docs/01-roadmap/product-positioning.md)

### 架构与系统说明

- [System Overview](Docs/02-architecture/system-overview.md)
- [Runtime Lifecycle](Docs/02-architecture/runtime-lifecycle.md)
- [Codebase Modules](Docs/02-architecture/codebase-modules.md)

### 模块专题

- [Memory & Retrieval](Docs/03-modules/memory-retrieval.md)
- [Week7 Acceptance Runbook](Docs/03-modules/week7-acceptance-runbook.md)
- [Observability & Replay](Docs/03-modules/observability-replay.md)
- [Tools & MCP](Docs/03-modules/tools-mcp.md)

### 治理与归档

- [ADR](Docs/adr)
- [Archive](Docs/archive)
- [SQL Scripts](Docs/sql)

## 当前建议的工作顺序

1. 先读 [Product Journey](Docs/01-roadmap/product-journey.md)，确认项目现在做到哪一步。
2. 再读 [System Overview](Docs/02-architecture/system-overview.md) 和 [Runtime Lifecycle](Docs/02-architecture/runtime-lifecycle.md)，建立整体图景。
3. 然后看 [Memory & Retrieval](Docs/03-modules/memory-retrieval.md)，确认 Week7 记忆体系。
4. 查看 [Week7 Acceptance Runbook](Docs/03-modules/week7-acceptance-runbook.md) 的已完成结论，然后进入 Week8 的 Daily Suggestion 实现。

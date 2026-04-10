# SKAgentSolution

产品级 Agent Runtime 项目，Week7 已完成并通过真实环境验收；Week8 foundation、Week8.x persona switching、Week8.5 多模型适配第一阶段均已完成真实环境验收。

## 当前定位

- 我们正在把项目从“能跑的 Agent Demo”推进到“可展示、可复现、可审计的产品级 Runtime”。
- Week7 已完成：Intent Router、Memory Fusion、pgvector、Recent Recall 和事件链路已通过验收。
- Week8.x 已验证通过：`default` 与 `coach` 双 persona 已可切换，且 `coach` 在对话与 Daily Suggestion 中已体现推进式风格。
- 当前优先级是继续深化 Week8 Daily Suggestion 的个性化质量，并把 `embedding / rerank / voice` 继续纳入统一模型路由。
- 关于为什么继续当前项目、它与 agent / claw-like 产品的关系、以及未来如何扩展，可参见 [Product Positioning](Docs/01-roadmap/product-positioning.md) 文档。

## 快速开始

1. 安装 .NET SDK（`net10.0`）。
2. 配置 `Src/SKAgent.Host/appsettings*.json`（至少 OpenAI；后续建议配置 `ConnectionStrings:PgVector`）。
3. 先阅读文档主线，确认当前阶段和结构。
4. 如需继续推进，请先看 Week7 验收结论，再查看 Week8 Daily Suggestions 的已验收状态与下一步优化方向。

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
- [Daily Suggestions](Docs/03-modules/daily-suggestions.md)
- [Observability & Replay](Docs/03-modules/observability-replay.md)
- [Model Routing](Docs/03-modules/model-routing.md)
- [Tools & MCP](Docs/03-modules/tools-mcp.md)

### 治理与归档

- [ADR](Docs/adr)
- [Archive](Docs/archive)
- [SQL Scripts](Docs/sql)

## 当前建议的工作顺序

1. 先读 [Product Journey](Docs/01-roadmap/product-journey.md)，确认项目现在做到哪一步。
2. 再读 [System Overview](Docs/02-architecture/system-overview.md) 和 [Runtime Lifecycle](Docs/02-architecture/runtime-lifecycle.md)，建立整体图景。
3. 然后看 [Memory & Retrieval](Docs/03-modules/memory-retrieval.md)，确认 Week7 记忆体系。
4. 查看 [Week7 Acceptance Runbook](Docs/03-modules/week7-acceptance-runbook.md) 的已完成结论，再读 [Daily Suggestions](Docs/03-modules/daily-suggestions.md) 进入 Week8 的 foundation 验收结果与后续优化阶段。

## 当前可直接调用的 Week8.x 入口

- `GET /api/personas`
- `GET /api/personas/current?conversationId=...`
- `POST /api/personas/current`
- `POST /api/suggestions/daily:run`
- `GET /api/suggestions`

当前默认策略：

- 新用户首次进入若未指定 persona，系统自动落到 `default`
- 当前会话的 persona 可通过 `POST /api/personas/current` 显式切换并持久化
- 当前已内置两个 persona：`default` 与 `coach`
- `coach` 更强调学习陪跑、下一步拆解和持续推进
- 同一天切换 persona 不会自动重算当天建议；`daily_suggestions` 继续按 `conversationId + date` 复用

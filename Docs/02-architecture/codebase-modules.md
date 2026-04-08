# Codebase Modules

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-03-31
- Related:
  - [System Overview](./system-overview.md)
  - [Runtime Lifecycle](./runtime-lifecycle.md)

## 文档目的

这份文档面向维护代码结构的人。它说明各项目、目录和模块应该承担什么职责，以及哪些依赖是允许的。

## 顶层模块分层

| 模块 | 责任 | 可以依赖 | 不应依赖 |
| --- | --- | --- | --- |
| Core | 领域模型、端口、共享契约 | 无或极少基础库 | Infrastructure、Host |
| Application | 编排、用例、策略、服务 | Core | Host |
| Runtime | 计划执行、运行时状态、反思、执行器 | Core、Application | Host、具体基础设施细节 |
| Infrastructure | 数据库、向量、模型、存储、外部实现 | Core、Application | Host 业务逻辑 |
| Host | API、DI、配置、启动项 | Core、Application、Runtime、Infrastructure | 反向被其他层依赖 |
| Tests | 契约、集成、回归验证 | 全部被测项目 | 生产逻辑被 Tests 反向引用 |

## 目录职责

### `Src/SKAgent.Core`

- 放稳定的领域模型和端口。
- 典型内容：Plan、RunContext、RetrievalPlan、PromptTarget、Memory / Fact / Vector 契约。
- 原则：越稳定的概念越应该在这里。

### `Src/SKAgent.Application`

- 放跨模块编排和业务策略。
- 典型内容：IntentRouter、RetrievalFusion、LongTermMemoryService、RunPreparationService。
- 原则：这里描述“如何协同工作”，不描述底层存储细节。

### `Src/SkAgent.Runtime`

- 放计划执行、工作记忆访问、执行器、反思器等运行时逻辑。
- 原则：保证一次 run 的状态机和执行流清晰可维护。

### `Src/SKAgent.Infrastructure`

- 放 Postgres / pgvector / Event Store / Embedding Provider / Fact Store 等实现。
- 原则：只提供实现，不改写业务边界。

### `Src/SKAgent.Host`

- 负责 API、依赖注入、配置和程序启动。
- 原则：Host 是装配点，不是业务中心。

### `Tests/SKAgent.Tests`

- 放契约测试、融合策略测试、集成测试模板。
- 原则：优先覆盖会漂移、会破坏产品行为的关键路径。

## 依赖方向

- `Host -> Runtime / Application / Infrastructure / Core`
- `Infrastructure -> Application / Core`
- `Runtime -> Application / Core`
- `Application -> Core`
- `Core -> 无上层依赖`

## 文档与代码对应关系

- 项目进度：见 [Product Journey](../01-roadmap/product-journey.md)
- 运行链路：见 [Runtime Lifecycle](./runtime-lifecycle.md)
- 记忆系统：见 [Memory & Retrieval](../03-modules/memory-retrieval.md)
- 事件与回放：见 [Observability & Replay](../03-modules/observability-replay.md)
- Tool / MCP：见 [Tools & MCP](../03-modules/tools-mcp.md)

## 结构约束

- 一个主题只能有一个权威主文档，其他文档只链接过去，不重复解释。
- 不允许在多个项目中复制同名同职责的实现。
- 新功能优先新增端口与策略，而不是直接把逻辑塞进 Host。
- 目录名、命名空间、文档术语必须尽量一致。

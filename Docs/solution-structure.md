# 解决方案结构（Week7）

## 目录约束
- `Src/SKAgent.Core`：只放抽象、契约、纯模型。
- `Src/SKAgent.Application`：编排与策略，不直接依赖 Host。
- `Src/SkAgent.Runtime`：运行时执行引擎与 SSOT。
- `Src/SKAgent.Infrastructure`：外部依赖适配（DB/MCP/SSE）。
- `Src/SKAgent.Host`：HTTP 接口与依赖注入。
- `Tests/SKAgent.Tests`：契约/融合/集成测试。

## Week7 新增关键模块
- `Core/Retrieval/*`：Intent、Plan、Fusion 契约。
- `Core/Memory/Facts/*`：Facts 双轨契约。
- `Application/Retrieval/*`：IntentRouter、QueryRewriter、RetrievalFusion。
- `Infrastructure/Memory/LongTerm/PgLongTermMemory.cs`：长期记忆读写实现。
- `Infrastructure/Memory/Facts/InMemoryFactStore.cs`：FactStore 默认实现。
- `Docs/sql/20260326_week7_pgvector.sql`：pgvector 脚本。

## 被移除的冲突项
- 重复 `InMemoryShortTermMemory`（仅保留 Infrastructure 实现）。
- 重复 `RunPreparation/RuntimePreparation`（仅保留 Core `RuntimePreparation`）。
- 错误命名空间 `IMemoryStore`（已删除）。
- Runtime Observability 错位命名空间（已归位）。

## 维护建议
使用以下命令生成结构快照并更新本文档：
```bash
rg --files Src Tests | sort
```

# SKAgentSolution

产品级 Agent Runtime Demo（SSOT + 可观测 + Week7 Memory 检索体系）。

## 当前能力（Week7）
- SSOT 运行时：`AgentRunContext` 统一承载 plan/step/tool/state。
- 记忆分层：short / working / facts / profile / vector。
- Intent Router：multi-label 意图识别，输出 `RetrievalPlan`。
- 多路融合：预算裁剪、去重、冲突规则（facts > recent > vector）。
- 长期记忆：pgvector（支持 NoOp 回退），`run_completed` 后入库。
- 可观测事件：intent、retrieval、vector、fusion、safety 全链路可回放。

## 快速开始
1. 安装 .NET SDK（`net10.0`）。
2. 配置 `Src/SKAgent.Host/appsettings*.json`（至少 OpenAI；推荐配置 `ConnectionStrings:PgVector`）。
3. 执行数据库脚本：`Docs/sql/20260326_week7_pgvector.sql`。
4. 运行 Host：
```bash
dotnet run --project Src/SKAgent.Host/SKAgent.Host.csproj
```

## API
- 非流式：`POST /api/agent/run`
- SSE：`POST /api/agentstream/run`

## 文档索引（SSOT）
- 架构：[Docs/architecture.md](Docs/architecture.md)
- Runtime 链路：[Docs/runtime.md](Docs/runtime.md)
- 事件模型：[Docs/observability.md](Docs/observability.md)
- 项目结构：[Docs/solution-structure.md](Docs/solution-structure.md)
- ADR：[Docs/adr](Docs/adr)

## 说明
- 当前仓库未内置数据库迁移工具，SQL 以脚本方式版本化管理。
- 若未配置 pgvector 连接串，系统会回退到 `NoOpLongTermMemory`，不影响主链路运行。

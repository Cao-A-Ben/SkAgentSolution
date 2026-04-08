# Week7 Acceptance Runbook

- Status: Completed
- Owner: Ben + Codex
- Last Updated: 2026-04-08
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [Memory & Retrieval](./memory-retrieval.md)
  - [Runtime Lifecycle](../02-architecture/runtime-lifecycle.md)
  - SQL: [20260326_week7_pgvector.sql](../sql/20260326_week7_pgvector.sql)

## 文档目的

这份 runbook 用于 Week7 的实际落地验收。它把“SQL 执行前检查”和“运行时验收步骤”放在同一个地方，避免你在代码、配置、SQL、接口之间反复跳。

当前状态：代码、测试、SQL 初始化和端到端验收均已完成，Week7 已收口。

## 当前实现基线

### 已确认对齐

- Host 配置通过 `ConnectionStrings:PgVector` 决定是否启用 pgvector。
- DI 中注册的 embedding provider 维度为 `128`。
- `PgVectorStore` 的表名、字段名、`content_hash` 去重、`conversation_id` 过滤与 SQL 脚本一致。
- `PgLongTermMemory` 查询与写入都走 `IVectorStore`。
- 当前 embedding 仍是离线哈希向量实现，模型标识为 `hash-embedding-v1-128`。

### 本地开发启动地址

- HTTP: `http://localhost:5192`
- HTTPS: `https://localhost:7108`

### 当前配置现状

- `appsettings.json` 中已经存在 `ConnectionStrings:PgVector`。
- 当前仓库里的 `OpenAI.ApiKey` 与数据库连接串是明文配置。

建议：在继续向外展示或提交前，把敏感配置迁移到环境变量或 User Secrets。

## 第 0 步：执行前硬检查

在执行 SQL 之前，先确认以下事项。

### 环境检查

- `dotnet build --no-restore` 已通过。
- `dotnet test --no-build` 已通过。
- 目标数据库可访问。
- 目标数据库不是会误伤生产数据的库。

### 数据库权限检查

执行本次脚本的数据库账号至少应具备：

- `CREATE EXTENSION` 权限，或由 DBA 预先安装 `vector`
- `CREATE TABLE`
- `CREATE INDEX`
- 对目标库的读写权限

### 实现对齐检查

- SQL 使用 `VECTOR(128)`，必须与当前 `EmbeddingProvider(dimension: 128)` 保持一致。
- 当前长期记忆以 `conversation_id` 作为优先过滤范围。
- 当前写入触发点在 `run_completed` 之后。

### 配置检查

确认以下配置已经存在：

```json
{
  "ConnectionStrings": {
    "PgVector": "Host=...;Port=5432;Database=...;Username=...;Password=..."
  }
}
```

如果 `PgVector` 为空，系统会回退到 `NoOpLongTermMemory`，Week7 长期记忆验收会失效。

## 第 1 步：执行 SQL 脚本（已完成）

### 脚本位置

- [20260326_week7_pgvector.sql](../sql/20260326_week7_pgvector.sql)

### 脚本作用

- 安装 `vector` 扩展
- 创建 `memory_chunks`
- 创建 `(conversation_id, ts)` 索引
- 创建向量索引，优先 `hnsw`，失败时回退 `ivfflat`

### 推荐执行方式

方式 1：使用 `psql`

```bash
psql "Host=... Port=5432 Dbname=... User Id=... Password=..." -f Docs/sql/20260326_week7_pgvector.sql
```

方式 2：在数据库客户端中直接执行脚本全文。

## 第 2 步：SQL 执行后验证

### 验证扩展

```sql
SELECT extname FROM pg_extension WHERE extname = 'vector';
```

预期：返回 `vector`

### 验证表结构

```sql
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'memory_chunks'
ORDER BY ordinal_position;
```

重点确认：

- `chunk_id`
- `conversation_id`
- `run_id`
- `persona`
- `ts`
- `content`
- `content_hash`
- `metadata`

`embedding` 列通常不会以普通 `data_type` 完整显示，可继续用下方语句验证。

```sql
SELECT pg_typeof(embedding) FROM memory_chunks LIMIT 1;
```

### 验证索引

```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'memory_chunks';
```

至少应看到：

- `idx_memory_chunks_conversation_ts`
- `idx_memory_chunks_embedding_hnsw` 或 `idx_memory_chunks_embedding_ivfflat`

## 第 3 步：启动 Host

```bash
dotnet run --project Src/SKAgent.Host/SKAgent.Host.csproj
```

开发环境默认地址：

- `http://localhost:5192`
- `https://localhost:7108`

## 第 4 步：非流式验收

### 请求示例

```json
{
  "conversationId": "week7-demo-001",
  "input": "记住：我最近的目标是完成 Week7 的 memory 功能，并且我偏好先整理文档再做数据库。"
}
```

### 调用示例

```bash
curl -X POST http://localhost:5192/api/agent/run \
  -H "Content-Type: application/json" \
  -d '{"conversationId":"week7-demo-001","input":"记住：我最近的目标是完成 Week7 的 memory 功能，并且我偏好先整理文档再做数据库。"}'
```

### 预期结果

- 返回 `conversationId`
- 返回 `runId`
- `status` 为成功完成
- `output` 有正常内容
- 若 profile 命中更新策略，可能包含 `profileSnapshot`

## 第 5 步：流式验收

### 调用示例

```bash
curl -N -X POST http://localhost:5192/api/agentstream/run \
  -H "Content-Type: application/json" \
  -d '{"conversationId":"week7-demo-001","input":"回忆一下我刚刚提到的目标和偏好。"}'
```

### 预期结果

SSE 中应逐步看到与本次 run 相关的事件输出。重点关注：

- `intent_classified`
- `retrieval_plan_built`
- `memory_fused`
- `run_completed`

如果 SQL 已正确执行，后续持久化阶段应出现：

- `vector_upserted`
- 视内容而定的 `fact_upserted` / `profile_updated`

## 第 6 步：长期记忆验收

### 场景 A：同会话回忆

在第一次写入后，再发起第二次请求：

```json
{
  "conversationId": "week7-demo-001",
  "input": "我之前的目标和偏好是什么？"
}
```

预期：

- 能回忆出“完成 Week7 memory 功能”
- 能回忆出“先整理文档再做数据库”
- 事件链中应包含长期记忆相关召回

### 场景 B：多意图

```json
{
  "conversationId": "week7-demo-001",
  "input": "顺便轻松聊聊，然后告诉我我之前的目标是什么。"
}
```

预期：

- 回复既保留闲聊语气，又完成 recall
- 事件中 intent 应体现 multi-label

### 场景 C：健康敏感

```json
{
  "conversationId": "week7-health-001",
  "input": "我最近总是失眠，还想试试一些刺激性很强的方法，之前有提过相关禁忌吗？"
}
```

预期：

- 触发 `health_sensitive`
- 出现 `safety_policy_applied`
- 输出包含免责声明或风险提示

## 第 7 步：数据库写入验证

在执行过至少一轮成功 run 后，查询：

```sql
SELECT conversation_id, run_id, persona, ts, left(content, 80) AS preview, content_hash
FROM memory_chunks
ORDER BY ts DESC
LIMIT 20;
```

预期：

- 至少能看到本次 `conversationId` 的片段
- `content_hash` 有值
- 重复执行相同写入内容时，不应无限插入重复行

### 去重验证

```sql
SELECT content_hash, COUNT(*)
FROM memory_chunks
GROUP BY content_hash
HAVING COUNT(*) > 1;
```

预期：无结果

## 第 8 步：向量召回验证

如果需要直接验证 conversation filter，可以执行：

```sql
SELECT conversation_id, content, ts
FROM memory_chunks
WHERE conversation_id = 'week7-demo-001'
ORDER BY ts DESC
LIMIT 10;
```

这一步先验证“写进去了”，再结合应用层回忆问题验证“能取出来”。

## 关键通过标准

### 代码与配置

- build 通过
- test 通过
- Host 可正常启动
- `PgVector` 连接串生效

### SQL 与存储

- `vector` 扩展存在
- `memory_chunks` 表存在
- 向量索引存在
- 写入成功且去重有效

### Runtime 行为

- `intent_classified` 事件存在
- `retrieval_plan_built` 事件存在
- `memory_fused` 事件存在
- `vector_upserted` 事件存在
- 多意图场景可工作
- recall 场景可工作
- health-sensitive 场景有安全策略事件

## 常见失败点

### 未启用长期记忆

症状：没有任何向量写入 / 查询事件。

排查：

- 检查 `ConnectionStrings:PgVector` 是否为空
- 检查 DI 是否走到了 `PgLongTermMemory` 而不是 `NoOpLongTermMemory`

### SQL 执行成功但仍无法写入

症状：应用可启动，但 `vector_upserted` 失败或无数据落库。

排查：

- 检查数据库权限
- 检查网络连通性
- 检查 `embedding VECTOR(128)` 是否与当前实现一致

### 能写不能回忆

症状：表里有数据，但 recall 不稳定。

排查：

- 检查是否使用了正确的 `conversationId`
- 检查 query rewriting 与 topK
- 检查事件中的裁剪与冲突规则

## 本轮结论

Week7 已在真实环境验收通过，关键证据如下：

- `recent_history_retrieved` 命中成功
- `recall_summary_built` 生成了正确候选：`你刚刚说了“你好啊”。`
- `run_completed.finalOutput` 最终稳定输出：`你刚刚说了“你好啊”。`
- `vector_upserted` 正常写入，说明长期记忆链路可用

本轮实际通过的代表性事件链：

- `intent_classified -> retrieval_plan_built -> recent_history_retrieved -> recall_summary_built -> memory_fused -> plan_created -> run_completed -> vector_upserted`

代表性最终输出：

```text
你刚刚说了“你好啊”。
```

结论：Week7 的 Intent Router、Recent History、Recall Summary、Memory Fusion、pgvector Upsert 和事件链路已经达到可演示、可复现、可审计的目标。

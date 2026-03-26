# Architecture（Week7）

## 目标
本架构面向“可展示、可复现、可审计”的产品级 Agent Runtime，强调：
- SSOT（单一事实源）
- 事件可观测
- 记忆与检索可解释

## 分层边界
- `SKAgent.Core`
  - 协议与模型：Planning、Runtime、Memory、Retrieval、Facts、Tools。
- `SKAgent.Application`
  - 用例编排：RunPreparation、MemoryOrchestrator、PromptComposer、Reflection。
- `SkAgent.Runtime`
  - 执行内核：`AgentRunContext`、`AgentRuntimeService`、`PlanExecutor`。
- `SKAgent.Infrastructure`
  - 外部适配：pgvector、SSE、MCP、Profile/Fact 存储。
- `SKAgent.Host`
  - API 入口与 DI 组装。

## Week7 核心设计
### 1) Intent Router 与 Planner 职责分离
- Intent Router：决定“要检索什么、预算如何分配、是否需要安全策略”。
- Planner：决定“执行步骤和工具选择”。
- 两者都产事件，回放时可解释。

### 2) Long-term Memory（pgvector）
- `run_completed` 后抽取文本 -> chunk -> 去重 -> upsert。
- 查询时 conversation scope 优先，向量召回结果参与融合。

### 3) 多路融合
- 路由来源：short / working / facts / profile / vector。
- 统一去重、预算裁剪、冲突决策。
- 冲突优先级：`facts > recent user statement > vector old`。

## 稳定扩展点（Week8~12）
- `PromptTarget` 预留 `Daily/Voice`。
- Retrieval/Facts 契约固定，后续仅增实现与策略。
- 事件模型保持向后兼容，便于回放 UI 与审计系统长期复用。

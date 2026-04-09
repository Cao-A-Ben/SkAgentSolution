# Product Journey

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-09
- Related:
  - [System Overview](../02-architecture/system-overview.md)
  - [Product Positioning](./product-positioning.md)
  - [Runtime Lifecycle](../02-architecture/runtime-lifecycle.md)
  - [Memory & Retrieval](../03-modules/memory-retrieval.md)

## 项目愿景

SkAgent 要从一个可运行的 Agent Demo，逐步演进成一个可展示、可复现、可审计、可继续扩展的产品级 Agent Runtime。这个项目不仅要能“回答问题”，还要具备稳定的运行时编排、可解释的记忆系统、可回放的事件链路、可控的工具调用，以及最终可以对外演示的 UI、语音、MCP 和领域 Skills。

这份文档是唯一的项目进度事实源。它覆盖 Week1 到项目收官的完整过程，既记录交付推进，也记录学习和架构收敛的轨迹。

## 总体阶段地图

| 阶段 | 时间范围 | 核心目标 | 当前状态 |
| --- | --- | --- | --- |
| 阶段 1 | Week1-Week3 | 建立最小 Agent 与基础工程骨架 | 已完成 |
| 阶段 2 | Week4-Week6 | 收敛 SSOT、Runtime、Profile、Observability | 已完成 |
| 阶段 3 | Week7-Week12 | 完成 Memory、UI、Voice、Repair、MCP、Skills | 进行中 |
| 阶段 4 | 收官阶段 | Demo、合规、复现性、文档交付 | 未开始 |

## Week1-Week3：认知搭建与最小 Agent

### 目标

- 搭起最小可运行的 Agent 工程。
- 完成基础分层，明确 Core / Application / Infrastructure / Host 的方向。
- 先把“能跑起来”建立为最小胜利，再逐步引入工程化能力。

### 关键交付

- 最小 API 入口。
- 基础对话能力与 Persona 驱动。
- Tool / Skill 的初步抽象。
- 解决方案级目录和项目依赖关系。

### 已完成

- 建立 .NET 解决方案与项目骨架。
- 初步跑通 Agent 请求与响应闭环。
- 引入基础 Persona、Prompt、Tool 组织方式。
- 形成最早期的实验性文档和设计草图。

### 未完成

- 当时尚未建立产品级 SSOT。
- Memory 仍偏原型化。
- 可观测性和回放能力尚未形成统一模型。

### 关键决策 / 教训

- 先要有可运行的最小闭环，再谈系统化重构。
- 早期允许实验，但必须在后续阶段收敛为单一事实源。

### 当前状态

- 作为历史基线已完成。

## Week4-Week6：SSOT、Runtime、Profile、Observability

### 目标

- 从“能跑”升级到“结构清晰、可解释、可维护”。
- 建立以 RunContext / Event 为中心的运行时单一事实源。
- 让 Profile、Prompt、Plan、Execution、Reflection 形成闭环。

### 关键交付

- SSOT Runtime 模型。
- RunPreparation / Planner / Executor / Reflection 基本链路。
- Event Store 与观测事件字典。
- Prompt 分层与可解释输入拼装。

### 已完成

- 收敛 RunContext、Plan、Step、Event 的运行时概念。
- 建立基础事件链路与 replay 方向。
- 增加 PromptComposer、PlanRequestFactory 等关键抽象。
- 引入 Profile / Facts 等长期事实结构的雏形。
- 初步整理 Memory 相关服务，减少随意耦合。

### 未完成

- Long-term Memory 仍未真正落地到可用向量检索。
- 多意图识别、多路检索融合、每日建议、语音等尚未接入。
- Repair Plan 与 MCP 仍停留在后续规划。

### 关键决策 / 教训

- Runtime 与 Observability 必须先于复杂能力，否则后续功能不可审计。
- Prompt、Plan、Memory 如果没有统一边界，会彼此漂移。

### 当前状态

- 已完成，为 Week7+ 提供了稳定地基。

## Week7-Week12：产品级能力成型

### 总目标

在 Week6 的 SSOT 和观测链路基础上，完成产品级 Demo 所需的关键能力：长期记忆、多路检索、每日建议、回放 UI、语音、Repair Plan、MCP、领域 Skills 与合规能力。

### Week7：Memory、Intent Router、pgvector、多路融合

#### 目标

- 让 Long-term Memory 从占位实现升级为真实可用能力。
- 引入 Intent Router 作为 Planner 的上游供给层。
- 用统一预算和冲突策略融合 short / working / facts / profile / vector。

#### 关键交付

- pgvector VectorStore 与长期记忆写入链路。
- RetrievalPlan、Intent Router、QueryRewriter、RetrievalFusion。
- Facts / Profile 双轨抗漂移策略。
- Week7 文档与 SQL 初始化脚本。

#### 已完成

- Week7 契约、Intent Router、RetrievalPlan、Memory Fusion 与 pgvector 链路已全部接入。
- pgvector SQL 已执行，长期记忆写入和召回已在真实环境通过。
- recent recall 已稳定输出：`你刚刚说了“你好啊”。`
- Week7 文档、runbook 和验收事件链已经归档为事实源。

#### 未完成

- 无阻塞项，Week7 作为阶段目标已完成。

#### 关键决策 / 教训

- Intent Router 和 Planner 不能争夺职责，必须分层。
- Memory 一旦开始产品化，文档必须先收敛，否则会出现“代码一套、文档一套、脑中一套”。

#### 当前状态

- 已完成。

### Week8：Daily Suggestion Job

#### 目标

- 支持每日建议自动生成、落库、可回放、失败可重试。

#### 关键交付

- DailySuggestionJob。
- SuggestionStore。
- 失败重试与 dead-letter 标记。

#### 已完成

- 完成 Week8 范围、调度方式、存储策略和作用域决策。
- DailySuggestionService、SuggestionStore、手动接口、Host 后台 Job、JSONL 回放指针和基础 SQL 已落地。
- 单测已覆盖“同一天不重复生成建议”的核心规则。

#### 未完成

- 真实数据库执行 Week8 SQL、手动 API 验收和后台调度启用验收尚未完成。

#### 关键决策 / 教训

- 每日建议必须复用 Runtime 与 Prompt 事实源，不能走旁路。

#### 当前状态

- 进行中。

### Week8.5：多模型适配层

#### 目标

- 让 planner / chat / embedding 可以独立选型，支持成本和稳定性优化。

#### 关键交付

- IChatModel / IEmbeddingModel。
- ModelRouter。
- 调用指标与 purpose 维度的选择事件。

#### 当前状态

- 规划中。

### Week9：UI 回放 + 语音对话 MVP

#### 目标

- 用 UI 展示 SSE timeline、历史 run 回放、tool / prompt / step。
- 接入 STT -> Runtime -> TTS 的语音闭环。

#### 当前状态

- 规划中。

### Week10：Reviewer + Repair Plan

#### 目标

- 从简单 retry 升级为可解释的修计划机制。

#### 当前状态

- 规划中。

### Week11：MCP 外部系统

#### 目标

- 将 MCP 作为 Tool 统一纳入 allowlist、权限、脱敏和审计体系。

#### 当前状态

- 规划中。

### Week12：领域 Skills + 合规 + Demo 收官

#### 目标

- 接入中医 / 技术领域 Skills，补足合规警示与最终可展示 Demo。

#### 当前状态

- 规划中。

## 收官阶段：Demo、合规、文档交付、复现性验证

### 目标

- 一键跑通产品级 Demo。
- 所有关键运行链路都可回放、可解释、可审计。
- 文档、图表、ADR、示例 run 完整交付。

### 关键交付

- Demo run 样例。
- 回放 UI 截图与流程说明。
- 最终架构文档与 ADR。
- 合规模板、免责声明与高风险提示。

### 当前状态

- 未开始。

## 当前产品定位

- SkAgent 当前定位见 [Product Positioning](./product-positioning.md)。
- 当前判断：SkAgent 属于 agents 项目，但更准确地说，它正在演进为 Product-grade Agent Runtime，而不是单纯的 claw-like 产品壳。
- 后续如果要扩展 skills 市场、安装机制或 marketplace，应放在 Week13+ 的 Claw Layer，而不是打断 Week7~12 的主线。

## 当前进度

- Week7 已完成，真实环境事件链和最终输出已验证通过。
- 文档主线已经稳定，可作为当前产品级事实源继续推进 Week8。
- 当前进入 Week8：Daily Suggestion Job 的设计与最小实现阶段。

## 当前阻塞

- Week8 foundation 已完成编码、编译和单测通过。
- 当前仍待完成真实库表执行与 `/api/suggestions` 端到端验收。

## 下一阶段计划

1. 执行 Week8 SQL：`daily_suggestions` 建表与索引。
2. 运行 Host，手动调用 `POST /api/suggestions/daily:run` 和 `GET /api/suggestions`。
3. 验证 `daily_job_started -> prompt_composed(target=daily) -> suggestion_saved -> daily_job_finished` 事件链。
4. 完成 Week8 foundation 提交，并继续进入真实建议质量调优。

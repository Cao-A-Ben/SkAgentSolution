# Product Journey

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-16
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

- 支持每日建议自动生成、落库、可回放、失败可补跑。

#### 关键交付

- DailySuggestionJob
- SuggestionStore
- 手动触发接口
- JSONL 回放指针

#### 已完成

- 完成 Week8 范围、调度方式、存储策略和作用域决策。
- DailySuggestionService、SuggestionStore、手动接口、Host 后台 Job、JSONL 回放指针和基础 SQL 已落地。
- `POST /api/suggestions/daily:run`、`GET /api/suggestions` 和 `daily_suggestions` 表已在真实环境通过。
- JSONL 事件链已验收通过，已确认 `daily_job_started -> prompt_composed(target=daily) -> suggestion_saved -> daily_job_finished` 闭环。
- Week8 当前已进入 foundation 之后的内容质量优化阶段，并已验证 `daily_suggestion_candidate_built` 能把建议收敛到更贴近项目推进的下一步。
- Daily Suggestion 的幂等维度已从 `persona_name + date` 升级为 `conversation_id + date`，更贴近后续多会话产品化方向。

#### 未完成

- 后台定时自动启用仍保持关闭，后续再做上线级验证。
- 失败重试与 dead-letter 仍保留在下一小阶段。

#### 关键决策 / 教训

- 每日建议必须复用 Runtime 与 Prompt 事实源，不能走旁路。
- 有了 foundation 以后，真正拉开产品差异的是建议内容质量，而不仅是定时任务本身。

#### 当前状态

- foundation 已完成并通过真实验收。

### Week8.5：多模型适配层

#### 目标

- 让 planner / chat / embedding 可以独立选型，支持成本和稳定性优化。

#### 关键交付

- IChatModel / IEmbeddingModel。
- ModelRouter。
- 调用指标与 purpose 维度的选择事件。

#### 已完成

- 已引入配置驱动的 `ModelRouter`，统一声明 `planner / chat / daily / embedding / rerank / voice` 的用途级路由。
- 已在真实环境验收 `planner / chat / daily` 的 `model_selected` 事件，并确认它们与实际调用模型一致。
- `coach` persona 与用途级模型路由已通过联合验收，说明 persona 风格与模型路由可以同时成立。
- `EmbeddingProvider` 已接入统一路由，当前会读取 `ModelRouting:Embedding` 的本地 hash 配置。
- `rerank` 已接入真实调用链，并已通过真实环境验收：`Recall -> vector -> rerank -> summary` 链路已建立。
- progress recall 已完成收口，并已通过真实环境验收：当记忆证据不足时，`recall_summary_built` 可使用 `recent_history+long_term+git_history` 生成多主题阶段总结。

#### 未完成

- `voice` 当前仍仅完成配置与契约预留，尚未接入真实调用链。
- 更细的模型调用指标与 provider-specific 审计信息留在下一小阶段。

#### 关键决策 / 教训

- `model_selected` 必须与真实调用一致，否则 observability 会失去可信度。
- `embedding` 当前虽然已统一走路由，但仍是离线 hash 实现；远程 embedding provider 要在下一阶段再真正落地。

#### 当前状态

- 已完成并通过真实环境验收。

### Week9：独立前端 Replay UI

#### 目标

- 新建独立前端工程，承接 run replay、timeline 和 daily suggestion replay。
- 让项目第一次拥有面向展示和调试的正式体验层，而不只是 API / SSE。

#### 关键交付

- 独立前端 Replay UI 工程。
- Replay API：run list、run detail、events、suggestion replay。
- timeline / prompt / steps / memory 摘要面板。
- Week9 Replay UI 专题文档与真实页面验收样例。

#### 当前状态

- 进行中。
- 已新增独立前端工程：`Frontend/SKAgent.ReplayApp`
- 已新增 Replay API：
  - `GET /api/replay/runs`
  - `GET /api/replay/runs/{runId}`
  - `GET /api/replay/runs/{runId}/events`
  - `GET /api/replay/suggestions`
- replay 已从“主要依赖 JSONL 文件定位”升级为 `PostgreSQL + JSONL` 混合架构：
  - `replay_runs` 负责元数据索引与 run list
  - JSONL 继续保留原始事件日志，供 timeline/detail 投影使用
- 普通 agent run、SSE run 与 daily suggestion run 均已进入同一套 replay 索引体系。

### Week10：Voice 对话 MVP

#### 目标

- 建立 `STT -> Runtime -> TTS` 的最小语音闭环。
- 让 `voice_stt / voice_tts` 从模型路由预留项升级为真实运行链路。

#### 当前状态

- 规划中。

### Week11：Reviewer + Repair Plan

#### 目标

- 从简单 retry 升级为可解释的修计划机制。

#### 当前状态

- 规划中。

### Week12：MCP / Skills / Demo 收口

#### 目标

- 将 MCP、领域 Skills、合规说明和最终 demo 路线收敛成一个可展示、可复现的收官阶段。

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
- 文档主线已经稳定，可作为当前产品级事实源继续推进 Week8 与 Week8.5。
- Week8 foundation 已完成，手动触发、落库、JSONL 回放指针、事件链与 conversation 维度幂等均已通过真实环境验收。
- Week8.x 已完成第二个真实 persona `coach`，并已通过真实环境验收：会话切换成功、对话输出体现“下一步引导”风格、次日 Daily Suggestion 也已体现推进式建议。
- Week8.5 已完成并通过真实环境验收：`planner / chat / daily / embedding / rerank` 的路由收敛已落地，progress recall 也已稳定收口为可解释的多主题阶段总结。
- Week9 第一阶段已打通：
  - 独立 Replay UI 工程已建立
  - Replay API 已可输出 `/api/replay/runs` 与 `/api/replay/suggestions`
  - replay 元数据已进入 PostgreSQL `replay_runs` 表，而不是继续停留在目录扫描方案

## 当前阻塞

- 当前主要剩余的是 Daily Suggestion 的进一步个性化、自动调度启用策略与失败补跑能力。
- Week9 已进入 UI 联调与 detail 视图收口阶段；当前剩余重点是前端真实页面验收、detail 接口联调与文档收口。
- `voice_stt / voice_tts` 仍未进入真实运行链路。

## 下一阶段计划

1. Week9 先建设独立前端 Replay UI，固定回放体验层和 Replay API 边界。
2. Week10 再接入 Voice 对话 MVP，使 `voice_stt / voice_tts` 成为真实运行能力。
3. 在 UI 与 Voice 之后推进 Reviewer + Repair Plan，减少多条高风险主线并行。
4. 继续增强 Daily Suggestion 的 persona/profile/facts 个性化，并在后续阶段评估自动调度与失败补跑。

# System Overview

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-10
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [Product Positioning](../01-roadmap/product-positioning.md)
  - [Runtime Lifecycle](./runtime-lifecycle.md)
  - [Codebase Modules](./codebase-modules.md)
  - Diagram source: [system-overview.mmd](../diagrams/system-overview.mmd)

## 文档目的

这份文档面向第一次接手项目的人。它回答四个问题：这个产品要解决什么问题、系统由哪些层组成、主数据流怎么走、当前系统做到哪一步。

## 产品定位

SkAgent 是一个产品级 Agent Runtime。它的目标不是单次对话，而是围绕“可解释的运行时”和“长期可演进的产品能力”构建一套完整平台，包括：

- Persona 驱动的对话与建议能力。
- 具备短期、工作记忆、事实、画像与向量记忆的多层记忆系统。
- 有计划、有执行、有回放的 Runtime。
- Tool / MCP / Skills 的可控扩展能力。
- 事件级可观测性、回放与审计。

补充判断：SkAgent 当前应被理解为 `Product-grade Agent Runtime`，而不是一个单纯依赖 skills 市场或安装机制的 claw-like 产品壳。后续如果引入 claw layer，也应建立在当前 Runtime 底座之上。

## 系统总览图

![SkAgent System Overview](../assets/diagrams/system-overview.svg)

阅读方式：从左到右看用户入口与 API，从上到下看系统分层，从中间主干看 Runtime 如何调用 Memory、Tools、Observability，并最终沉淀为回放和长期记忆。

## 分层架构

### Experience Layer

- API 与 SSE 面向对话、流式输出、后续 UI 回放、语音入口。
- 未来会接入 Web UI、Voice Panel、Replay UI。

### Runtime Layer

- RunPreparation 负责准备输入、识别意图、构造 RetrievalPlan。
- Planner 负责生成步骤计划。
- Executor 负责执行 steps、tools、agents。
- Reviewer / Reflection 负责失败处理、修复、总结与后置沉淀。

### Memory & Tooling Layer

- Memory 拆成 short、working、facts、profile、vector 五类事实源。
- Tooling 统一走 Tool 协议，未来扩展到 MCP、外部系统、领域 Skills。

### Platform Layer

- Infrastructure 提供 Postgres / pgvector、Embedding、Event Store、Suggestion Store 等实现。
- Observability 记录事件、指标、脱敏结果和回放素材。

## 主数据流

1. 用户通过 API 或流式接口发起请求。
2. Runtime 进入 RunPreparation，识别意图并构建 RetrievalPlan。
3. 多路记忆被按预算召回和融合，形成 Planner / Chat 的上下文。
4. Planner 产出 AgentPlan，Executor 执行步骤与工具。
5. 运行过程中的事件被持续写入事件链路，以支持回放、审计与分析。
6. `run_completed` 之后，输出被提取为事实、画像更新和向量片段，进入长期存储。

## 当前系统边界

### 已稳定建立

- Core / Application / Runtime / Infrastructure / Host 分层。
- 基础 Planner / Executor / Reflection 链路。
- Week7 的 Intent Router、RetrievalPlan、Memory Fusion、pgvector 契约。
- 事件驱动的观测与回放基础。

### 正在推进

- Week8 Daily Suggestion foundation：建议生成、落库、手动接口、JSONL 回放指针，且已完成真实环境验收。
- Week8.x Persona 入口正式化：已补充 persona 查询与会话级切换接口，并将 persona 绑定接入后续对话主链路。
- Week8.x 第二 persona：`coach` 已落地，并已通过真实环境验收，persona 切换已从流程能力升级为可展示能力。
- Week8.5 多模型适配层：第一阶段已完成，`planner / chat / daily` 的 `model_selected` 已通过真实环境验收并与实际调用对齐；`embedding` 已纳入统一路由的本地 hash 配置；`rerank / voice` 仍为预留配置。
- UI 回放、语音、Repair Plan、MCP、领域 Skills。

### 暂未落地

- Daily Suggestion 的内容质量进一步个性化与自动调度启用策略。
- `rerank / voice` 的真实模型接入与更细的模型审计字段。
- 外部 MCP 适配器与权限体系。

## 设计原则

- 单一事实源优先，避免 Runtime、Memory、Docs 各自为政。
- Planner 管动作，Intent Router 管信息供给与风险控制。
- 事件是运行时的第一公民，任何关键决策都要可回放。
- 文档不是补充材料，而是产品交付的一部分。

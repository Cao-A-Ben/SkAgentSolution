# Product Positioning

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-04-02
- Related:
  - [Product Journey](./product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Memory & Retrieval](../03-modules/memory-retrieval.md)
  - [Week7 Acceptance Runbook](../03-modules/week7-acceptance-runbook.md)

## 文档目的

这份文档用于回答一个关键问题：在 `agent`、`agent runtime`、`skills/tool`、`claw-like product` 这些概念越来越混杂的当下，SkAgent 到底在做什么、为什么值得继续、以及未来应该怎样扩展。

它不是架构文档，也不是周计划文档，而是项目的定位说明和后续升级基线。后续如果再讨论是否要做 skills 市场、安装机制、权限体系或 marketplace，都以这份文档为决策起点。

## 核心结论

### 结论 1

SkAgent 当然属于 `agents` 项目，但它不是停留在“单个智能体演示”层面，而是在往 `Product-grade Agent Runtime` 演进。

### 结论 2

`claw-like product` 不是对 `agent` 的替代，而是站在 agent 之上的产品层和生态层。它更强调分发、安装、skills 扩展、权限控制和用户体验。

### 结论 3

SkAgent 当前最值得坚持的方向，不是半路改造成一个“技能市场产品壳”，而是先把 `runtime + memory + replay + observability + compliance` 这套底座打扎实。

### 结论 4

如果未来要往 claw-like 方向演进，正确方式不是推翻现有项目，而是在 Week7~12 完成后，于现有 Runtime 之上增加 `Skill Layer / Marketplace Layer / Permission Layer`。

## 术语澄清

### Agent

`Agent` 指能够基于模型、上下文和工具完成任务的智能行为体。它关注的是“能不能理解目标、做计划、调用工具并给出结果”。

### Agent Runtime

`Agent Runtime` 指承载 agent 运行的底层系统，包括：

- 输入准备
- memory 检索
- planner / executor
- tool 调度
- reflection / repair
- event / replay / audit

它关注的是“agent 如何稳定、可解释、可观测地运行”。

### Skill / Tool

`Skill` 或 `Tool` 是 agent 可以调用的能力单元。它们可能是：

- 内置函数能力
- 外部系统连接器
- 工作流模板
- 领域规则与内容模板

它关注的是“agent 能做哪些扩展动作”。

### Claw-like Product

本文中的 `Claw-like Product` 指一种建立在 agent 之上的产品层。它通常更强调：

- skills 的安装与管理
- capability marketplace
- permission / allowlist
- 用户交互壳与分发体验
- 生态和商业化能力

它关注的是“能力如何被组织、交付、安装、扩展和运营”。

## Agent 与 Claw 的抽象对比

### Agent 更偏能力内核

Agent 侧更关注：

- 理解输入
- 构造计划
- 调度工具
- 利用记忆
- 处理错误

也就是说，它首先解决的是“会不会做事”。

### Claw-like Product 更偏产品壳与生态层

Claw-like 侧更关注：

- skills 如何安装
- capabilities 如何分发
- 权限如何控制
- 市场如何发现与复用
- 用户如何低门槛使用

它首先解决的是“能力怎么被打包、扩展、运营和商业化”。

### 两者不是替代关系

更准确的理解应该是：

- `Agent` 是能力核心
- `Agent Runtime` 是运行底座
- `Claw-like Product` 是建立在 Runtime 之上的产品层

因此，`claw-like product` 可以包含 agent 的很多外显功能，但并不等于它天然拥有更扎实的 runtime、memory、replay 或 audit 能力。

### 本项目与两者的关系

SkAgent 当前明显更接近：

- `Agent Runtime`
- `Product-grade Agent Infrastructure`

而不是一个只强调 skills 市场或安装壳的产品。

## SkAgent 当前定位

### 不是什么

SkAgent 不是：

- 一个只会聊天的单 agent demo
- 一个只靠 prompt 拼装的试验项目
- 一个单纯面向 skills 市场的产品壳

### 是什么

SkAgent 是：

- 一个面向产品级交付的 Agent Runtime 底座
- 一个具备长期记忆、回放、可观测和后续合规能力的系统
- 一个未来能够承接 skills / MCP / voice / marketplace 扩展的基础平台

### 为什么这个定位成立

当前项目已经具备或正在形成这些核心能力：

- SSOT Runtime
- Planner / Executor / Reflection
- 多层 Memory
- pgvector 长期记忆路线
- Event / Replay / Audit 基线
- MCP、Voice、Repair、Skills 的后续扩展钩子

这些能力说明，SkAgent 的价值不在“会不会装 skills”，而在“能否成为一个可持续演进的 agent 系统底座”。

## 为什么继续当前项目

### 当前已经形成的壁垒

SkAgent 已经开始建立以下高价值能力：

- Runtime 边界清晰
- 事件链路可回放
- Memory 分层明确
- Long-term Memory 与 Facts / Profile 正在收敛
- 后续可以承接 repair、voice、MCP、compliance

这些能力的建设难度和长期价值，通常高于一个早期产品壳的 skills 展示能力。

### 不应该半路转去追产品壳

如果现在为了追“claw 热度”而直接切成 marketplace 或 skills 安装产品，会带来几个问题：

- Runtime 地基还没完全做完
- 观测、回放、合规这些真正的底层壁垒会被打断
- 项目会从长期底座建设，退化成短期产品模仿

因此，更稳妥的路径不是改道，而是继续当前项目，把它做成一个真正能承载未来 claw-like 扩展的 Runtime。

### 继续当前项目的现实意义

坚持当前路线意味着：

- 你不是在重复做一个已经很多人都在做的“技能壳”
- 你是在做一个更底层、更难、但更有产品壁垒的基础系统
- 未来如果要做 skills 市场，你会是“有底座的人”，而不是“只有壳的人”

## 后续升级路线

### Phase A：继续完成 Week7~12

当前阶段仍然应聚焦既定主线，不改变任务顺序：

- Week7：Memory、Intent Router、pgvector、多路融合
- Week8：Daily Suggestion Job
- Week8.5：多模型适配层
- Week9：独立前端 Replay UI
- Week10：Voice 对话 MVP
- Week11：Reviewer + Repair Plan
- Week12：MCP / Skills / Demo 收口

这一阶段的目标不是做市场壳，而是把 SkAgent 的核心 runtime 做到可展示、可复现、可审计。

### Phase B：Week13+ 引入 Claw Layer

在 Week12 收官后，如果底座稳定，可以新增一个扩展阶段：

`Week13+ / Phase B: Claw Layer`

这个阶段的目标是：在不推翻现有 Runtime 的前提下，增加产品壳和生态层。

## 未来扩展清单

### Skill Layer

- Skill package format
- Skill manifest
- Skill metadata
- Skill versioning
- Skill install / uninstall flow

### Marketplace Layer

- Skill registry
- Marketplace / discovery UI
- 分类、标签、推荐与搜索
- 官方 skills 与社区 skills 的区分

### Permission Layer

- Skill permission model
- Tool / MCP allowlist
- Scope-based external access
- 审计与风险提示

### Distribution Layer

- Skill distribution channel
- 可下载 capability bundles
- 环境安装与同步机制
- 组织级能力包管理

### Product Layer

- 面向用户的能力管理 UI
- Skill enable / disable
- 配置页、凭证页、运行权限页
- 生态化的 onboarding 与模板化入口

## 何时再评估方向

这份定位不是永久不变的，但不应在 Week7 中途反复摇摆。

建议的下一次正式定位评审时间点是：

- Week12 收官后
- Demo、回放、语音、MCP、合规都已有第一版结果之后

到那时再判断：

- 是继续强化 Runtime 深度
- 还是进入 Week13+ 的 Claw Layer 实施
- 或者两条线并行推进

## 与现有主文档的边界

- `product-positioning.md`：回答为什么做、是什么、往哪走
- `product-journey.md`：记录从 Week1 到收官的阶段推进
- `system-overview.md`：描述当前系统分层与主数据流
- `week7-acceptance-runbook.md`：指导如何执行 SQL 和做 Week7 验收

这份文档不重复展开架构细节，也不替代 roadmap 或 runbook。

## 当前建议

当前最合理的项目判断是：

- 继续当前项目
- 不因 claw-like 概念升温而改道
- 先完成 Week7~12 的 Runtime 基线建设
- 再在其之上规划 Week13+ 的产品壳和生态层

换句话说，SkAgent 现在更应该成为：

`一个能承载未来 claw-like 产品层的 Product-grade Agent Runtime`。

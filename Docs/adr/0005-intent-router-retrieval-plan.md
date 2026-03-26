# ADR-0005: Intent Router 作为 Planner 上游供给层

## Status
Accepted

## Context
Planner 负责动作序列；检索路由负责信息供给与风险控制。若二者混在同一层，容易出现意图冲突、预算失控和不可解释行为。

## Decision
- 引入 Intent Router（rule-first）作为 RunPreparation 阶段能力。
- Router 输出 multi-label `RetrievalIntent` 与 `RetrievalPlan`（routes、budgets、topK、safetyPolicy）。
- Planner 消费已增强的上下文，不再承担检索预算决策。
- Router 与 Fusion 全部产出结构化事件，支持回放解释。

## Consequences
Positive:
- 多意图输入可稳定分路（闲聊 + 回忆 + 工具）。
- 检索路径、裁剪和冲突可解释。
- Week8~12 功能增量无需频繁改 Planner 契约。

Negative:
- 运行时阶段更长，需要更严格事件治理与预算控制。

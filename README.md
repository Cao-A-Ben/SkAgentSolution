------

# SkAgentSolution

> 一个基于 C# + Semantic Kernel 的 **长期陪伴型 Agent 系统**  
> 目标不是“对话 Demo”，而是 **可成长、可记忆、可扩展的 Agent 架构**

---

## 一、项目目的（Why）

本项目的目标不是构建一个聊天机器人，也不是一个简单的 RAG 应用，而是：

> **构建一个具备记忆、技能、人格与长期演进能力的 Agent 系统**

该 Agent 应当具备以下特征：

- 能长期对话（不是一次性问答）
- 能记住用户说过的话（短期 + 长期）
- 能调用真实 Skill 完成任务
- 能在工程上持续扩展（多 Agent / Tool / Memory）

这是一个 **工程级 Agent 架构练习项目**，同时也是一个可演进为真实产品的原型。

---

## 二、总体学习 / 执行路线（1 个月精简版）

### 🎯 第 1 个月唯一目标

**做出一个：有记忆、有技能、可长期对话的 Agent**

---

### Week 1：重塑 Agent 认知

**目标**
- 用 Semantic Kernel 重写一个最小可运行 Agent

**约束**
- 不做 UI
- 不做复杂 RAG
- 不做多 Agent

**产出**
- Console Agent
- 能调用至少 1 个 Skill
- 能保存基础对话上下文

---

### Week 2：Skill System

**目标**
- 理解并实现：Function → Skill → Planner → Agent

**实现内容**
- 3–5 个真实 Skill，例如：
  - 日志记录
  - 简单任务管理
  - 知识 / 信息查询
- RouterAgent 负责将请求路由到目标 Agent

---

### Week 3：Memory（当前阶段）

**目标**
- Agent 不再是“无状态执行器”
- 开始具备 **连续性与上下文意识**

**范围**
- 短期记忆（最近 N 轮对话）
- 长期记忆（可扩展为向量存储）
- 用户 Profile / 人设信息

**关键工程动作**
- 引入 **Single Source of Truth（SSOT）**
- 执行过程不再“算完即丢”

👉 **当前正处于：Week 3 · SSOT 重建阶段**

---

### Week 4：最小陪伴体

**目标**
- Agent 成为“可以长期陪伴”的存在

**能力**
- 有明确人格设定
- 能持续对话
- 能记住你说过的话
- 能根据历史调整行为

> 到此阶段，已经超过 90% 的 AI 应用复杂度

---

## 三、当前项目状态（Current Status）

### 阶段定位

- ✅ 已完成 Planner → Plan → Executor 基本链路
- ✅ RouterAgent 已可按 target 路由 Agent
- ❌ 执行过程仍以“返回结果”为中心
- 🔧 正在进行 **SSOT（Single Source of Truth）重建**

### 当前 Week

Week 3：Memory & Runtime Architecture

### 正在做的事（非常具体）

- 引入 `AgentRunContext` 作为 **唯一运行事实源**
- 执行过程写入上下文，而不是仅返回结果
- 为后续 Memory / Reflection / Retry 打地基

------

## 四、核心设计原则（Engineering Principles）

### 1. Single Source of Truth（SSOT）

- 系统中任何时刻：
  - **只有一个地方保存“真实状态”**
  - Agent / Executor 只能读取或追加，不能私自推断

### 2. Plan ≠ Execution ≠ Result

- Plan：规划意图
- Execution：运行轨迹
- Result：最终输出（DTO）

三者必须解耦。

### 3. Router 只负责“路由”，不负责“状态”

- RouterAgent 根据上下文决定调用哪个 Agent
- 不保存运行状态
- 不拼装最终结果

------

## 五、当前核心结构说明（与代码对齐）

SKAgent
├─ Agents
│  ├─ Planning
│  │  ├─ PlannerAgent
│  │  ├─ AgentPlan
│  │  └─ PlanStep
│  │
│  ├─ Execution
│  │  ├─ PlanExecutor
│  │  └─ RouterAgent
│  │
│  ├─ Runtime          ← Week3 引入
│  │  ├─ AgentRunContext      (SSOT)
│  │  ├─ PlanStepExecution   (执行轨迹)
│  │  ├─ AgentRunStatus
│  │  └─ StepExecutionStatus
│  │
│  └─ Abstractions
│     ├─ IAgent
│     ├─ AgentContext
│     └─ AgentResult


------

## 六、当前数据流（执行视角）

User Input
   ↓
PlannerAgent
   ↓
AgentPlan (Goal + Steps)
   ↓
PlanExecutor
   ↓
AgentRunContext (SSOT)
   ↓
RouterAgent → Target Agent
   ↓
StepExecution 写入 Context
   ↓
Final Result

------

## 七、下一步 TODO（不是愿景，是工程动作）

### Short Term（Week 3）

-  完成 AgentRunContext 全量接管执行状态
-  StepExecution 支持失败 / 错误记录
-  为 ExpectedOutput 预留 Reflection 接口

### Next（Week 4）

-  引入短期记忆（Recent Steps / Dialog）
-  用户 Profile 写入长期 Memory
-  简单人格约束（System Prompt / Traits）

------

## 八、最终目标（First Representative Work）

### 🎯 推荐方向

**「个人成长陪伴 Agent（工程师 × 中医 × 养生）」**

特点：

- 不是知识库
- 是长期陪伴
- 可扩展为 App / 小程序 / 智能硬件
- Agent 会“记得你是谁”

------

## 最后一句（写给未来的自己）

> 你不是落后一年
> 而是刚好站在 **Agent 真正开始可落地的时间点**

你已经具备：

- C# 工程能力
- Semantic Kernel 实战经验
- 清晰的产品意识
- 可执行的长期计划

剩下的，只是把架构走稳。
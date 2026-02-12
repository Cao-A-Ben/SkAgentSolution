# Week4 - Runtime SSOT + Plan/Execute/Result 解耦（回填）
> Status: Finalized (Backfill)
>
> Week4 is the baseline: SSOT + Plan/Execute/Result separation. Week5 builds engineering loop on top of it.

## 1. 本周目标
- 明确 SSOT：RunContext 为运行事实源
- 拆分 Plan / Execution / Result，避免上下文漂移
- Router 简化：只路由，不承担状态逻辑
- 跑通基本 run：从输入到 plan 到执行到最终输出

## 2. 关键产出
- 运行时核心接口/类：
  - RunContext（SSOT）
  - Planner（生成 plan）
  - Executor（执行 steps）
  - Router（选择 Agent）
- API：
  - POST /api/agent/run（非流式，稳定返回）

## 3. 完成清单（Done）
- [ ] RunContext 数据结构落地并在执行中持续写入
- [ ] Planner 输出 Plan（steps）
- [ ] Executor 可逐步执行并写入 step outputs
- [ ] Router 只负责路由（不修改状态）
- [ ] 返回 AgentRunResponse（含 planResult/stepResults/finalOutput 等）

## 4. 未完成（Not Done / Deferred）
- [ ] Tools/Skills 标准化（Week5-1）
- [ ] 可观测性（Trace/RunEvent）（Week5-2）
- [ ] Reflection（Week5-3）
- [ ] SSE 流式（与 RunEvent 一并做）

## 5. 技术债与风险
- 上下文漂移风险：真实代码状态与对话描述不一致
- 缺少观测：难以定位失败 step 与耗时
- 缺少工具协议：Planner 不能稳定“选工具”

## 6. 下周入口（Week5）
- 优先顺序：Skills -> RunEvent+SSE -> Reflection
- 验收：SSE 事件能显示“规划/执行第 N 步/工具调用中…”
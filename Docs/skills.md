# Skills / Tools
> Status: Draft (Week5-1)
>
> Unifies tool registration/discovery/invocation so the Planner can choose tools and Executor can execute them deterministically.
>
> Related:
> - ADR-0002
> - docs/observability.md (tool events)

目标：统一 Tool/Skill 的“注册-发现-选择-调用-观测”协议，使 Planner 能选择工具，Executor 能稳定执行并产生可观测事件。

## 1. 术语

- Tool / Skill：这里统一称 Tool。Skill 可以是 Tool 的语义层封装。
- ToolDescriptor：工具声明（name/description/schema/capability）
- ToolInvocation：一次调用（toolName + args）
- ToolResult：结果（success/output/error/metrics）

## 2. 设计原则

- 统一协议：HTTP/DB/MCP/本地函数 都必须映射为 Tool
- 参数显式：必须有 input schema（最小也要有 JSON 结构约束）
- 可观测：调用前后必须 emit tool_* 事件（见 observability）
- 错误可控：ToolResult 必须封装 error（code/message/details）

## 3. ToolDescriptor（建议字段）

- name（稳定唯一，建议 namespace 前缀，如 `http.get`, `db.query`, `mcp.search`）
- description（给 Planner 用）
- inputSchema（JSON Schema 或自定义简单 schema）
- outputSchema（可选）
- tags（可选：capability/safety/dataSensitivity）
- timeoutMs（可选）
- idempotent（可选：重试策略会用）

## 4. ToolRegistry

- Register(ToolDescriptor, ITool)
- ListAll() / FindByName(name)
- ListByTag(tag)（可选）

Planner 在生成 plan 时需要从 registry 得到可用工具清单（建议只给摘要：name + description + inputSchema 摘要）。

## 5. Tool Invocation Contract

- 输入：
  - ToolInvocation：runId、stepId、toolName、args
- 输出：
  - ToolResult：success、output、error、metrics（latencyMs 等）

ToolInvoker 必须负责：
- 统一超时控制
- 统一异常捕获 -> ToolResult.error
- 统一事件：tool_invoked/tool_completed

## 6. Planner 如何选择工具（最小实现）

Planner 在 step 中可产生：
- ToolStep：
  - toolName
  - args
  - expectedOutput（可选）

Executor 遇到 ToolStep：
- 调用 ToolInvoker(toolName, args)
- 将结果写入 RunContext.steps[stepId].output
- emit step_completed（同时已 emit tool_*）

## 7. 验收标准（Week5-1）

- 至少实现一种 Tool Adapter（例如 HttpTool 或本地 FunctionTool）
- Planner 能产出 ToolStep
- Executor 能执行 ToolStep 并写入 RunContext
- 事件流中可看到 tool_invoked/tool_completed

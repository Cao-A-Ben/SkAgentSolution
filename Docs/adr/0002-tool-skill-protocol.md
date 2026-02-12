# ADR-0002: Unified Tool/Skill Protocol

## Status
Accepted

## Context
系统需要支持多种能力接入（HTTP/DB/MCP/本地函数）。若每种能力各自实现调用/错误/日志，将导致：
- Planner 无法统一选择工具
- Executor 难以统一执行与重试
- 可观测性割裂

## Decision
采用统一 Tool 协议：
- ToolDescriptor：声明（name/description/schema/tags）
- ToolInvocation：一次调用（toolName + args）
- ToolResult：输出（success/output/error/metrics）
- ToolRegistry：统一注册与发现
- ToolInvoker：统一执行、超时、错误封装与事件输出

## Consequences
Positive:
- Planner 可生成 ToolStep
- Executor 可稳定执行与观测
Negative:
- 需要维护 schema/tags 的一致性

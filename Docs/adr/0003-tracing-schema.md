# ADR-0003: Run Event Model as Unified Observability Schema (Trace + SSE)

## Status
Accepted

## Context
仅 token 流无法表达工具调用、检索与反思等阶段；同时项目需要工程级可观测性（trace、回放、指标）。
如果分别实现 Trace 与 SSE，将导致协议重复与维护成本上升。

## Decision
以 RunEvent 为统一事件模型：
- 所有运行事实以 RunEvent emit（run/plan/step/tool/chat/reflection）
- Trace/日志/JSONL/SSE 均为 RunEvent 的 Exporters（投影）
- SSE 使用事件流传递进度，token（chat_delta）作为一种事件类型

## Consequences
Positive:
- 体验与工程观测统一
- 可回放与断言（测试友好）
Negative:
- 需要治理 payload 脱敏
- 需要维护事件版本兼容（后续可加 schemaVersion）

# Replay UI

- Status: Week9 UI Implemented, Acceptance Ready
- Owner: Ben + Codex
- Last Updated: 2026-04-17
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](./observability-replay.md)
  - [Daily Suggestions](./daily-suggestions.md)

## 文档目的

这份文档定义 Week9 的唯一目标：建设一个**独立前端 Replay UI**。它用于固定 Week9 的体验层边界、后端 API 依赖、页面结构和验收标准，让新的会话窗口可以直接进入实施。

## Week9 固定目标

- 新建独立前端工程，不采用 Blazor。
- 前端专门承接 replay、timeline、run detail 和 daily suggestion replay。
- Week9 只做 Replay UI，不混入 Voice 对话 MVP。

## 工程位置

- 建议前端工程放在仓库顶层独立目录，例如：
  - `Frontend/SKAgent.ReplayApp`
- Host 继续保留现有 ASP.NET Web API 职责：
  - 提供对话、SSE、persona、suggestion、replay API
- Week9 不在 Host 内嵌 UI，不采用服务器端页面技术。

## 后端 API 依赖

Week9 文档中默认后端需要提供或稳定化这些接口：

- `GET /api/replay/runs`
- `GET /api/replay/runs/{runId}`
- `GET /api/replay/runs/{runId}/events`
- `GET /api/replay/suggestions`

这些接口的目标是统一读取：

- 普通 run 的 replay 数据
- Daily Suggestion 的 replay 数据
- 与 replay 直接相关的 summary / metadata

## 当前实现快照

当前仓库已进入 Week9 的验收前状态：

- 已新增独立前端工程：
  - `Frontend/SKAgent.ReplayApp`
- Host 已新增 Replay API：
  - `GET /api/replay/runs`
  - `GET /api/replay/runs/{runId}`
  - `GET /api/replay/runs/{runId}/events`
  - `GET /api/replay/suggestions`
- 普通 `POST /api/agent/run` 与 `POST /api/agent-stream/run` 已补上 JSONL replay 落盘。
- PostgreSQL 现已承担 `replay_runs` 元数据索引表，列表与 `runId -> eventLogPath` 定位不再依赖目录扫描。
- Daily Suggestion 继续沿用 Week8 的 `eventLogPath` 指针，不改已验收语义。
- `prompt_composed` 已补充 prompt 文本快照字段，供 Run Detail 的 Prompt 面板直接展示。
- `/api/replay/runs` 与 `/api/replay/suggestions` 已完成真实接口验证。
- `GET /api/replay/runs/{runId}` 与 `GET /api/replay/runs/{runId}/events` 已完成真实接口验证。
- `dotnet build` 与 `dotnet test` 已通过；当前重点转入前端页面联调与 detail 视图验收。
- `Frontend/SKAgent.ReplayApp` 已完成 `npm run build` 验证。
- 当前前端已补齐：
  - 中英文双语切换
  - runs / suggestions 刷新入口
  - run / suggestion 顶部统计卡
  - runs / suggestions 列表 + sticky preview rail
  - run detail 双栏布局 + sticky summary rail
  - run detail milestone 事件概览
  - timeline 优先级分层与 payload preview
  - timeline / steps / memory 空态
  - suggestion 无 replay 时的禁用态
  - Week9 acceptance 命中矩阵、signals 与 risk watch
- Playwright 浏览器自动化检查暂未完成，当前环境下载 Chromium 较慢；Week9 先以真实 API 验证 + 前端构建验证 + 手工页面联调作为当前收口路径。

## 当前 UI 完成范围

### Runs

- 最近 run 列表
- 搜索与 `kind / status` 过滤
- 右侧 preview rail：
  - run 身份摘要
  - acceptance coverage
  - trace depth
  - replay signals
  - risk watch
  - recent events

### Run Detail

- 双栏布局
- sticky summary rail
- timeline 过滤、优先级标签、payload preview
- prompt / steps / memory 诊断面板
- milestone 命中概览

### Suggestions

- Daily Suggestion 历史
- 列表 + preview rail
- replay trace / diagnostics
- 跳转对应 replay

## 开发态 API 访问方式

Replay UI 本地开发默认运行在：

- `http://127.0.0.1:4179`

开发态前端并不直接把浏览器请求发到 `5192`，而是采用 Vite 代理：

- 浏览器请求：`http://127.0.0.1:4179/api/...`
- Vite proxy 转发到：`http://127.0.0.1:5192/api/...`

默认代理目标在：

- `Frontend/SKAgent.ReplayApp/vite.config.ts`

其中：

- `process.env.SKAGENT_HOST` 只是 Vite 启动时读取的代理目标地址
- 未设置时默认回落到 `http://127.0.0.1:5192`
- 当前前端没有静态 JSON fallback，也没有 mock replay 数据路径

如果浏览器 Network 面板里能看到：

- 请求来自 `4179`
- 响应头包含 `server: Kestrel`

则说明链路是：

- 浏览器 -> Replay UI dev server -> Host API

## 页面结构

### Runs List

- 展示最近 runs 列表。
- 关键信息：
  - `runId`
  - `conversationId`
  - `personaName`
  - `status`
  - `startedAt / finishedAt`

### Run Detail

- 展示单次 run 的 timeline 与详情。
- 默认包含这些区域：
  - `Summary`
  - `Events`
  - `Prompt`
  - `Steps`
  - `Memory`

### Suggestions List

- 展示 Daily Suggestion 历史。
- 每条建议都能跳转到对应 replay。

## Timeline 最小覆盖事件

- `run_started`
- `persona_selected`
- `intent_classified`
- `retrieval_plan_built`
- `vector_query_executed`
- `model_selected`
- `prompt_composed`
- `plan_created`
- `step_started`
- `step_completed`
- `run_completed`
- `daily_job_started`
- `suggestion_saved`
- `daily_job_finished`
- `recall_summary_built`

## 与 Voice 的边界

Week9 不包含：

- `STT / TTS`
- 麦克风输入
- 音频播放链路
- `voice_stt / voice_tts` 的真实接入

这些内容统一顺延到 Week10。

## 验收标准

- 独立前端工程可启动并访问现有 Replay API。
- 能展示至少一条 progress recall run 的 timeline。
- 能展示至少一条 daily suggestion run 的 timeline。
- 页面上能直接看到：
  - `persona_selected`
  - `model_selected`
  - `prompt_composed`
  - `recall_summary_built`
  - `suggestion_saved`
  - `daily_job_finished`
- 实现者在 Week9 不需要再决定：
  - UI 是否独立工程
  - 是否采用 Blazor
  - Voice 是否与 UI 并行

## Week9 最终验收清单

- `GET /api/replay/runs`
- `GET /api/replay/runs/{runId}`
- `GET /api/replay/runs/{runId}/events`
- `GET /api/replay/suggestions`
- `/runs`
  - 可展示 run list
  - 可搜索和过滤
  - preview rail 可显示 acceptance / signals / risk / activity
- `/runs/:runId`
  - 可展示 timeline / prompt / steps / memory
  - timeline 中可见关键事件：
    - `persona_selected`
    - `model_selected`
    - `prompt_composed`
    - `recall_summary_built`
    - `suggestion_saved`
    - `daily_job_finished`
- `/suggestions`
  - 可展示 suggestion history
  - 可跳转对应 replay
  - preview rail 可展示 diagnostics 与 replay trace
- 至少完成两条真实样例复验：
  - 一条 progress recall run
  - 一条 daily suggestion run

## 当前手工联调入口

- Host 默认地址：
  - `http://127.0.0.1:5192`
- Replay UI 本地开发：
  - `cd Frontend/SKAgent.ReplayApp`
  - `npm install`
  - `npm run dev`
- 建议手工检查页面：
  - `http://127.0.0.1:4179/runs`
  - `http://127.0.0.1:4179/runs/245cc2f5b1de4daa965d9eebcd36a3dc`
  - `http://127.0.0.1:4179/suggestions`

## 当前默认结论

- Week9 = 独立前端 Replay UI
- Week10 = Voice 对话 MVP
- Week9 的重点是正式建立体验层，而不是在现有 Host 上临时挂一个页面

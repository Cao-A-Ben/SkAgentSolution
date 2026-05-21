# Demo Runbook

- Status: Active SSOT
- Owner: Ben + Codex
- Last Updated: 2026-05-20
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [Observability & Replay](./observability-replay.md)
  - [Voice Runtime](./voice-runtime.md)
  - [Tools & MCP](./tools-mcp.md)
  - [Repair Plan](./repair-plan.md)

## Purpose

这份 runbook 固定 Week12 的最终 demo 路线，目标不是覆盖所有能力，而是保证一条完整、可解释、可回放、可复现的演示路径。

## Demo Scope

固定演示顺序：

1. 文本对话
2. replay
3. daily suggestion
4. voice
5. MCP / skill 示例

Week12 不要求：

- 正式接入真实第三方 MCP 生产系统
- 自动执行 repair step
- 大规模前端重构

## Environment Checklist

- Host 可启动：`dotnet run --project Src/SKAgent.Host/SKAgent.Host.csproj`
- Replay UI 可启动：
  ```bash
  cd Frontend/SKAgent.ReplayApp
  npm run dev
  ```
- 默认地址：
  - Host API: `http://127.0.0.1:5192`
  - Replay UI: `http://127.0.0.1:4179`
- 如需 voice 演示：
  - 本地 STT 已可用
  - 本地 TTS 已可用
  - `POST /api/voice/run` 可返回 `mp3`

## Sample Capture Script

如果你希望一次性固定 Week12 的 demo 样例、`runId` 和回放证据，可直接运行：

```bash
Scripts/week12/capture-demo-samples.sh
```

如需同时采样语音 run：

```bash
Scripts/week12/capture-demo-samples.sh --voice-file voice-sample.wav
```

脚本会：

- 调用 `GET /api/skills`
- 生成 text / daily / skill 样例
- 可选生成 voice 样例
- 自动抓取对应 replay detail 与 events
- 在 `artifacts/week12-demo/<timestamp>/summary.md` 里输出：
  - `runId`
  - 建议截图位点
  - demo 讲解词

注意：

- 最稳妥的做法是让 Host 和采样脚本运行在同一侧环境里。
- 如果是混合环境，例如 Host 跑在 Windows shell、脚本跑在 WSL bash，可能会遇到 `localhost` 不互通；这时请显式传 `--host-url`，或直接在与 Host 相同的 shell 中运行脚本。

## Demo Route

### 1. Text Chat

目标：证明普通文本 run 仍然是主链路，Week12 新增能力没有绕开统一 runtime。

请求示例：

```bash
curl -X POST http://127.0.0.1:5192/api/agent/run \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "demo-week12-chat-001",
    "input": "请总结当前项目 Week11 已完成了什么，并给出接下来 Week12 的一个最小推进建议。"
  }'
```

讲解点：

- 仍然走统一 `run_started -> prompt_composed -> plan_created -> step_* -> run_completed`
- 没有 skill 也能稳定运行
- replay detail 中 `skill` 区块为空是符合预期的

### 2. Replay

目标：证明 demo 不是黑盒输出，而是可回放、可解释。

操作：

1. 打开 Replay UI
2. 进入刚才的 `runId`
3. 检查：
   - timeline
   - prompt
   - steps
   - memory
   - repair
   - skill

讲解点：

- Week11 的 repair 面板仍然可见
- Week12 新增的 `skill_selected` 会进入同一条 timeline，而不是旁路日志
- run detail 现在会单独投影 `skill` 摘要

建议截图：

- run detail 总览
- timeline 中的关键 milestone
- prompt / skill / repair 三个面板

### 3. Daily Suggestion

目标：证明 daily suggestion 继续复用统一 replay 与索引体系。

请求示例：

```bash
curl -X POST http://127.0.0.1:5192/api/suggestions/daily:run \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "demo-week12-daily-001"
  }'
```

讲解点：

- 结果会进入 `GET /api/suggestions`
- daily run 也能在 Replay UI 中被回放
- 这条路线是 Week8/Week8.x/Week8.5 和 Week9 的复用，不是 Week12 的旁路功能

建议截图：

- suggestions 列表页
- 对应 suggestion replay 入口

### 4. Voice

目标：证明语音闭环已经是现有 runtime 的一个输入/输出编排层。

请求示例：

```bash
curl -X POST http://127.0.0.1:5192/api/voice/run \
  -F "conversationId=demo-week12-voice-001" \
  -F "audio=@voice-sample.wav"
```

讲解点：

- `voice_input_received`
- `voice_transcribed`
- Agent runtime 文本链路
- `voice_response_synthesized`
- `voice_playback_ready`

建议截图或证据：

- 返回的音频文件信息
- 对应 replay timeline

### 5. MCP / Skill Example

目标：证明外部工具治理、skill 选择、planner hint、tool allowlist、audit 事件、replay 投影已经打通。

先查看可用 skill：

```bash
curl http://127.0.0.1:5192/api/skills
```

示例请求：

```bash
curl -X POST http://127.0.0.1:5192/api/agent/run \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "demo-week12-skill-001",
    "skillName": "tech.mcp_demo",
    "input": "请用最小演示路径说明 MCP demo tool 是如何接入统一 external tool / audit / replay 链路的。"
  }'
```

预期观察点：

- `skill_selected`
- planner 看到 skill hint
- planner 优先生成 `mcp.demo_echo` 的 tool step
- 如工具执行：
  - `external_call_started`
  - `external_call_finished`
- 如策略阻断：
  - `external_call_blocked`
  - 可进入 Week11 repair 路径

讲解点：

- skill 不是第二套 runtime，只是统一 runtime 上的产品级能力包
- external tool governance 同时影响 planner 可见性和执行期兜底
- replay detail 的 `skill` 面板可以直接显示：
  - skill name
  - display name
  - source
  - recommended tools

建议截图：

- `GET /api/skills` 返回
- replay timeline 中的 `skill_selected`
- run detail 的 skill 面板
- 如有 external call，则保留对应 audit 事件截图

## Suggested Evidence Set

建议至少保留以下材料：

1. 文本 run 的 `runId`
2. daily suggestion 的 `runId`
3. voice run 的 `runId`
4. MCP / skill run 的 `runId`
5. Replay UI 截图：
   - text run detail
   - repair panel
   - skill panel
   - external call events

## Acceptance Notes

Week12 demo 通过的最低标准：

- 一条完整 demo 路线可复现
- 文本 / replay / daily / voice / MCP-skill 五段都能讲清楚
- 新增 Week12 能力仍然进入统一 observability / replay 体系

如果需要正式对外交付，还应补充：

- 录屏脚本
- 截图命名规范
- 环境初始化说明
- 外部依赖检查清单

# Voice Runtime

- Status: Week10 Phase 1 In Progress
- Owner: Ben + Codex
- Last Updated: 2026-04-17
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Model Routing](./model-routing.md)
  - [Observability & Replay](./observability-replay.md)

## 文档目的

这份文档定义 Week10 的服务端语音闭环范围。当前目标不是做复杂语音前端，而是把 `STT -> Runtime -> TTS` 严肃地挂到现有 Runtime 上，并继续保留 replay / model routing / observability 的一致性。

## 当前实现范围

- 已新增 `POST /api/voice/run`
- 请求形态：
  - `multipart/form-data`
  - 必填字段：`audio`
  - 可选字段：`conversationId`、`personaName`、`voice`、`format`
- 当前服务端闭环：
  - `voice_input_received`
  - `model_selected purpose=voice_stt`
  - `voice_transcribed`
  - 复用现有 Agent Runtime
  - `model_selected purpose=voice_tts`
  - `voice_response_synthesized`
  - `voice_playback_ready`

## 当前分层

- `SKAgent.Core`
  - `IVoiceTranscriptionService`
  - `IVoiceSynthesisService`
  - `IVoiceAgentRuntime`
  - `VoiceRuntimeOptions`
- `SKAgent.Application`
  - `VoiceRuntimeService`
  - 负责串联 STT / Runtime / TTS 用例
- `SkAgent.Runtime / Host`
  - `AgentVoiceRuntimeAdapter`
  - 负责把语音用例接到现有 `AgentRuntimeService`
- `SKAgent.Infrastructure`
  - `OpenAiCompatibleVoiceGateway`
  - 当前默认 provider 实现

## 当前配置入口

- `VoiceGateway:BaseUrl`
- `VoiceGateway:ApiKey`
- `VoiceGateway:TimeoutSeconds`
- 若 `VoiceGateway` 留空，则回退到通用 `OpenAI:BaseUrl` / `OpenAI:ApiKey`
- 这样 Week10 可以先保留一套默认 openai-compatible 实现，同时允许语音链路单独切换到别的网关或账号
- 当前默认 `voice_stt` 已指向本地 `faster-whisper-server`：
  - `http://127.0.0.1:8000/v1`
- `KokoroTts:BaseUrl`
- `KokoroTts:ApiKey`
- 当前默认 TTS 已切到 `kokoro-local`，按本地 OpenAI-compatible `POST /v1/audio/speech` 端点对接
- 当前已验证一套可用的 Kokoro 服务：
  - `GET /v1/models`
  - `GET /v1/voices`
  - `POST /v1/audio/speech`

## 为什么当前默认实现是 openai-compatible

- Week10 需要先形成一条真实可跑的默认链路。
- 但语音能力不应被某一家 provider 锁死，因此上层只依赖抽象接口。
- 后续可以替换或新增：
  - 本地 Whisper / Vosk
  - 腾讯
  - Google
  - 其他兼容 OpenAI Audio API 的服务

## 当前响应形态

- 当前 `POST /api/voice/run` 返回：
  - `conversationId`
  - `runId`
  - `transcript`
  - `outputText`
  - `audioBase64`
  - `audioContentType`
  - `audioFormat`

说明：

- 这是 Week10 MVP 的最小响应，优先保证联调与验收效率。
- 后续若需要前端直接流式播放或分段播放，可以再演进为二进制音频流或下载地址方案。

## 当前验收目标

- 一次音频上传能完成：
  - 转写
  - 进入现有 Runtime
  - 生成文本回复
  - 合成可播放音频
- replay 中能看到：
  - `voice_input_received`
  - `voice_transcribed`
  - `voice_response_synthesized`
  - `voice_playback_ready`
- `model_selected` 中：
  - `purpose=voice_stt`
  - `purpose=voice_tts`
  - 与真实调用 provider / model 一致

## 当前真实联调结论

- `POST /api/voice/run` 已完成真实外部调用验证
- 当前 `api.routin.ai` 这组凭证会把语音请求转发到外部网关，但返回：
  - `no_available_provider`
- Host 当前已把这类外部 provider 故障收敛成：
  - `502 Bad Gateway`
  - `application/problem+json`
- 这意味着 Week10 当前已经打通：
  - 语音入口
  - provider-neutral 编排
  - 对外语音网关调用
  - 标准化错误面
- 后续只需切换到一组真正具备语音后端能力的 `VoiceGateway` 配置，即可继续完成 STT / TTS 真正闭环验收

## 当前推荐本地 TTS 方案

- 当前推荐把 `voice_tts` 切到本地 Kokoro
- 当前工程默认约定本地 Kokoro 服务暴露 OpenAI-compatible `POST /v1/audio/speech`
- `alloy / echo / fable / onyx / nova / shimmer` 这类 OpenAI voice alias 可直接映射到 Kokoro voice
- 这样中间链路可以稳定保持：
  - 文本输入 -> Runtime -> Kokoro TTS
  - 语音输入 -> STT/ASR -> Runtime -> Kokoro TTS

## 当前本地 STT 状态

- 当前默认 `voice_stt` 已切到本地 `faster-whisper-server`
- 接口兼容 OpenAI `POST /v1/audio/transcriptions`
- 已确认：
  - `/health` 正常
  - `/v1/models` 正常
  - `Systran/faster-whisper-small` 能加载进服务
- 当前剩余风险不在 Host 路由，而在本地 STT 服务自己的推理/模型状态：
  - `small` 模型请求存在较长等待
  - `tiny` 模型请求当前返回 `Internal Server Error`

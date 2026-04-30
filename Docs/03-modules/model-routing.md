# Model Routing

- Status: Week8.5 Accepted, Week10 In Progress
- Owner: Ben + Codex
- Last Updated: 2026-04-17
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](./observability-replay.md)
  - [Daily Suggestions](./daily-suggestions.md)

## 文档目的

这份文档描述 Week8.5 引入的用途级模型路由。它说明不同 purpose 如何映射到不同 provider/model，以及哪些路由已经接入真实调用链，哪些仍属于配置预留。

## 当前结论

- `planner / chat / daily` 已通过真实环境验收，并确认 `model_selected` 与实际调用模型一致。
- `embedding` 已接入统一路由，但当前仅支持 `local + hash-embedding-v1-*` 的离线实现。
- `rerank` 已接入真实调用链，并已通过真实环境验收。
- `voice_stt / voice_tts` 已进入 Week10 第一阶段实现：
  - 已有真实代码接缝
  - 当前默认 provider 为 `openai-compatible`
  - 尚待真实环境完整验收

## 当前默认路由

| Purpose | 当前默认模型 | 当前状态 |
| --- | --- | --- |
| `planner` | `gpt-4o-mini` | 已接入并验收 |
| `chat` | `gpt-4o` | 已接入并验收 |
| `daily` | `gpt-4o-mini` | 已接入并验收 |
| `embedding` | `hash-embedding-v1-128` | 已接入本地 hash 路由 |
| `rerank` | `gpt-4o-mini` | 已接入并验收 |
| `voice_stt` | `Systran/faster-whisper-small` | Week10 第一阶段已接线，默认对接本地 faster-whisper-server |
| `voice_tts` | `tts-1` | Week10 第一阶段已接线，默认对接本地 Kokoro |

## 真实验收样例

### 对话链路

- `persona_selected.source = request`
- `model_selected purpose=planner -> gpt-4o-mini`
- `model_selected purpose=chat -> gpt-4o`

### Daily 链路

- `daily_job_started`
- `persona_selected.source = request`
- `daily_suggestion_candidate_built`
- `model_selected purpose=daily -> gpt-4o-mini`
- `prompt_composed target=daily`
- `daily_job_finished created=true`

### Recall + Rerank 链路

- `intent_classified = Recall`
- `vector_query_executed`
- `model_selected purpose=rerank -> gpt-4o-mini`
- `rerank_applied`
- `recall_summary_built`

## 设计边界

- `model_selected` 现在是可信审计事件，不应再只代表“计划中的选择”。
- `embedding` 当前仍是离线 hash 实现，因此即使走统一路由，也不会假装支持尚未落地的远程 embedding provider。
- `rerank` 已是真实能力，不再只是预留配置。
- `voice_stt / voice_tts` 现已进入真实代码链路，但当前仍以最小闭环为目标，后续可继续扩充 provider-specific 审计字段。
- 当前 `voice_stt` 默认已切到本地 `faster-whisper-server`，仍通过 openai-compatible 协议接入。
- 当前 `voice_tts` 默认已切到 `kokoro-local + tts-1`，约定下游是兼容 `POST /v1/audio/speech` 的本地 Kokoro 服务。

## 下一步

1. 为 `embedding` 增加更清晰的 provider 能力边界和远程实现扩展点。
2. 为 Week10 的 `voice_stt / voice_tts` 补充真实环境验收样例。
3. 如果后续需要更细粒度审计，再补充 provider-specific 的 token / latency 指标。

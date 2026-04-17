# Model Routing

- Status: Week8.5 Accepted
- Owner: Ben + Codex
- Last Updated: 2026-04-15
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
- `voice_stt / voice_tts` 仍为配置位和默认值，等待 Week10 的真实接入。

## 当前默认路由

| Purpose | 当前默认模型 | 当前状态 |
| --- | --- | --- |
| `planner` | `gpt-4o-mini` | 已接入并验收 |
| `chat` | `gpt-4o` | 已接入并验收 |
| `daily` | `gpt-4o-mini` | 已接入并验收 |
| `embedding` | `hash-embedding-v1-128` | 已接入本地 hash 路由 |
| `rerank` | `gpt-4o-mini` | 已接入并验收 |
| `voice_stt` | `gpt-4o-mini` | Week10 目标 |
| `voice_tts` | `gpt-4o-mini` | Week10 目标 |

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
- `voice_stt / voice_tts` 仍先保留配置与目的枚举，等 Week10 的真实能力接入时再把事件链补齐。

## 下一步

1. 为 `embedding` 增加更清晰的 provider 能力边界和远程实现扩展点。
2. 在 Week10 接入 `voice_stt / voice_tts` 后补充 `model_selected` 的验收样例。
3. 如果后续需要更细粒度审计，再补充 provider-specific 的 token / latency 指标。

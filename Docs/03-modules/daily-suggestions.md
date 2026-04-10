# Daily Suggestions

- Status: Week8 Foundation Accepted
- Owner: Ben + Codex
- Last Updated: 2026-04-09
- Related:
  - [Product Journey](../01-roadmap/product-journey.md)
  - [System Overview](../02-architecture/system-overview.md)
  - [Observability & Replay](./observability-replay.md)
  - [Memory & Retrieval](./memory-retrieval.md)
- SQL: [20260409_week8_daily_suggestions.sql](../sql/20260409_week8_daily_suggestions.sql)
  - Migration: [20260409_week8_daily_suggestions_conversation_scope.sql](../sql/20260409_week8_daily_suggestions_conversation_scope.sql)

## 文档目的

这份文档记录 Week8 `Daily Suggestion Job` 的产品目标、数据流、存储结构、事件链和真实验收结果。它的作用是保证“每日建议”复用同一套 Runtime、Memory、Prompt 与 Replay 事实源，而不是演变成一条不可审计的旁路能力。

## Week8 目标

- 每天生成一条建议
- 建议可落库
- 建议与 `runId`、`promptHash`、`eventLogPath` 绑定
- 支持手动触发
- 后台调度默认关闭，但具备启用能力
- 同一天重复调用不重复生成

## 当前状态

### Foundation 已完成并通过真实验收

- `SuggestionRecord`、`ISuggestionStore`、`IConversationScopeResolver` 契约已建立
- `DailySuggestionService` 已复用现有 `RunPreparationService + PromptComposer + Memory` 链路
- `PromptTarget.Daily` 已接入 `model_selected + prompt_composed(target=daily)`
- Postgres `SqlSuggestionStore` 已可落库，内存回退实现已保留
- `POST /api/suggestions/daily:run` 与 `GET /api/suggestions` 已在真实环境通过
- `JsonlRunEventSink + JsonlRunEventLogFactory` 已接入，建议记录会保存 `eventLogPath`
- `DailySuggestionJob` 已作为 Host 层后台任务接入，默认 `Enabled=false`
- 单测已覆盖“同一天重复调用不重复生成”与“过滤元回忆输入、优先项目上下文”两类规则

### 已确认的真实验收结果

- `POST /api/suggestions/daily:run` 成功返回建议、`runId`、`promptHash`、`eventLogPath`
- 第二次同日调用返回 `created=false`
- `GET /api/suggestions` 能返回已落库建议
- 2026-04-12 的真实结果已验证 `daily_suggestion_candidate_built` 生效，建议内容从泛化的“回顾对话”收敛为更贴近当前项目推进的下一步
- JSONL 事件链已确认包含：
  - `daily_job_started`
  - `persona_selected`
  - `vector_query_executed`
  - `recent_history_retrieved`
  - `memory_retrieved_long_term`
  - `memory_fused`
  - `daily_suggestion_candidate_built`
  - `model_selected`
  - `prompt_composed`
  - `suggestion_saved`
  - `run_completed`
  - `daily_job_finished`

## 数据流

1. 解析目标日期、persona 与 conversation scope
2. 若当天建议已存在，则直接返回现有记录
3. 创建独立的 daily run context，并绑定 JSONL 事件日志
4. 加载最近会话、画像、persona 与 retrieval plan
5. 调用 `RunPreparationService` 生成 `PromptTarget.Daily`
6. 通过文本生成服务生成建议正文
7. 保存 `daily_suggestions` 记录
8. 写入 `daily_job_started -> prompt_composed(target=daily) -> suggestion_saved -> daily_job_finished` 事件链

## 存储模型

`daily_suggestions` 的核心字段：

- `suggestion_date`
- `suggestion`
- `run_id`
- `conversation_id`
- `persona_name`
- `profile_hash`
- `prompt_hash`
- `created_at`
- `event_log_path`

设计原则：

- `daily_suggestions` 是建议结果表，不是原始事件表
- 原始事件仍落到 JSONL，保证 replay 指针与结果记录解耦
- 当前唯一键按 `suggestion_date + conversation_id` 控制幂等
- 这比 `suggestion_date + persona_name` 更适合多会话场景，也更接近后续多用户产品化方向

## API

### `POST /api/suggestions/daily:run`

用途：手动触发当天或指定日期的建议生成。

请求体：

```json
{
  "date": "2026-04-09",
  "personaName": "default",
  "conversationId": "optional-conversation-id"
}
```

返回：

- `date`
- `suggestion`
- `runId`
- `conversationId`
- `personaName`
- `promptHash`
- `profileHash`
- `eventLogPath`
- `created`

### `GET /api/suggestions`

用途：读取最近建议记录列表，供后续 UI / replay 入口使用。

### `GET /api/personas`

用途：列出当前可用 persona，供前端或调用方展示选择器。

### `GET /api/personas/current?conversationId=...`

用途：查看某个会话当前会使用哪个 persona。
说明：

- 如果会话已经绑定 persona，返回 `source=store`
- 如果会话尚未绑定，返回 `source=default`
- 这个查询接口本身不会写入绑定关系

### `POST /api/personas/current`

用途：按 `conversationId` 显式切换当前会话的 persona。
说明：

- 如果 persona 不存在，返回业务错误
- 如果当前绑定 persona 不允许切换，返回冲突错误
- 如果切换到与当前相同的 persona，返回幂等成功，`changed=false`
- 成功切换后会写入 conversation 绑定，并影响后续 `/api/agent/run` 与后续日期的 Daily Suggestion
- 同一天切换 persona 不会自动重算当天 `daily_suggestions`

## Persona 与新用户

### 当前实现

- `personaName` 不是写死在系统里的单一值，它是一个 persona 模板名
- 当前选择优先级是：`request -> conversation store -> default`
- 如果请求里显式传了 `personaName`，系统会尝试按名称查找对应 persona
- 如果不传，系统会优先取当前 `conversationId` 已绑定的 persona；没有绑定时再回退到 `default`

### 为什么 `笨笨` 会报错

- 当前 persona 来源是 `personas/*.json`
- 只有已经存在的 persona 名称才能被使用
- 当前已内置 `default` 与 `coach`
- `default` 可以直接用，`笨笨` 之所以失败，是因为仓库里还没有对应的 persona 定义文件

### 新用户现在怎么接

- 新用户第一次进入时，可以不传 `personaName`
- 系统会给这个新会话自动选中 `default`
- 该 persona 会按当前策略绑定到这个 `conversationId`
- Daily Suggestion 的幂等也会跟着这个 `conversationId` 走，而不是只看 persona 名称
- 现在也可以通过 `GET /api/personas` 和 `GET /api/personas/current` 让前端显式展示“有哪些 persona / 当前会落到哪个 persona”
- Week8.x 进一步增加了 `POST /api/personas/current`，让 persona 切换不再只是概念，而是正式的会话入口能力

### 当前 persona 定位

- `default`
  - 通用、自然、简洁
  - 更适合首次进入和轻量闲聊
- `coach`
  - 更偏学习陪跑与项目推进
  - 更强调拆解下一步、保持节奏、减少泛化鼓励
  - 更适合 Week8.x 之后的主动建议和推进式对话

### `coach` 验收样例

建议按这组顺序做真实验收：

1. 切换 persona

```json
POST /api/personas/current
{
  "conversationId": "260c5ee21d48445b9c3d61ab30dde1ef",
  "personaName": "coach"
}
```

2. 用同一会话继续对话

```json
POST /api/agent/run
{
  "conversationId": "260c5ee21d48445b9c3d61ab30dde1ef",
  "personaName": "coach",
  "input": "你好，我想继续推进 Week8.x"
}
```

预期：

- `persona_selected.source = request` 或 `store`
- 回复比 `default` 更偏“下一步澄清 / 陪跑推进 / 具体动作”

3. 用新日期生成建议

```json
POST /api/suggestions/daily:run
{
  "date": "2026-04-14",
  "conversationId": "260c5ee21d48445b9c3d61ab30dde1ef",
  "personaName": "coach"
}
```

预期：

- suggestion 更像“今天先做什么”
- 同日再次调用仍按 `conversationId + date` 复用

真实验收结果：

- 切换 persona 返回：
  - `personaName = coach`
  - `changed = true`
- 对话输出示例：
  - `你好！请问你在 Week8.x 中具体想实现什么目标或完成哪些任务？这样我可以帮助你制定下一步行动。`
- `2026-04-14` 的 Daily Suggestion 示例：
  - `今天先整理 Week8 已验证通过的结果，再挑一个最关键的优化点继续推进。`

### 产品级后续方向

- 现在的实现更适合单用户/演示阶段
- 真正面向多用户时，建议把 persona 选择拆成：
  - `userId`
  - `conversationId`
  - `default persona`
  - `user-level persona preference`
- 到那时，`personaName` 更像“可选覆盖项”，而不是每次请求都必须手填的主入口
- 后续如果引入 `userId`，建议把 Daily Suggestion 再进一步升级为 `user_id + suggestion_date` 或 `user_id + conversation_id + suggestion_date`

## 调度策略

- 默认 `Enabled=false`，避免开发环境一启动就自动落库
- `RunOnStartupIfMissing=true`，当显式开启后台任务后，如果当天缺失记录，可在启动时补跑一次
- `UseLatestConversation=true`，当前演示阶段默认使用最近活跃会话作为建议上下文来源

## 事件与回放

Week8 关键事件：

- `daily_job_started`
- `prompt_composed(target=daily)`
- `suggestion_saved`
- `daily_job_finished`
- `daily_job_failed`

回放原则：

- 用 `SuggestionRecord.runId` 定位某一次 daily run
- 用 `eventLogPath` 打开完整事件 JSONL
- 用 `promptHash` 确认当次提示词版本

## 内容质量方向

当前 foundation 已经可用，但内容质量仍有继续优化空间：

- 更强地利用 recent history，而不是输出泛化鼓励
- 更强地利用 profile / facts，让建议更贴近用户长期目标
- 更清晰地区分“项目推进建议”和“生活方式建议”
- 后续可在 Week8.5 配合 `ModelPurpose.Daily` 做模型策略优化

## 当前边界

### 已明确不做

- 暂不做复杂 dead-letter 队列
- 暂不做通知渠道推送
- 暂不做多用户调度中心
- 暂不做建议质量打分和 rerank

### 后续可扩展

- Week8.x：继续提升 prompt 和内容质量
- Week8.x：继续验证 `coach` persona 的切换效果与次日建议差异
- Week8.5：接入 `ModelPurpose.Daily` 的独立模型策略
- Week9：在 UI 中展示建议历史和 replay 入口
- Week11 之后：把 daily suggestion 扩展为 MCP / tools 增强的主动任务

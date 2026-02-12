# Week4 - 最小陪伴体（Persona + Profile + 会话级 API）

## 目标
在 Week3（SSOT + Trace + ShortTermMemory）基础上，完成最小陪伴体：
- Persona：系统级人格约束（影响 Chat + Planner）
- User Profile：可写可读画像（比短期记忆稳定）
- 会话级 API：DTO 化，客户端只需保存 conversationId 即可持续对话

## 交付清单
- [x] PersonaOptions + PersonaCatalog
- [x] ChatAgent 使用 ChatHistory SystemMessage 注入 persona
- [x] IUserProfileStore（InMemory）
- [x] RuntimeService：run 前读取 profile + recent_turns；run 后更新 profile；响应返回 profileSnapshot
- [x] API 改为 DTO：{ conversationId?, input } -> { conversationId, runId, output, steps, profileSnapshot }

## 当前架构（核心流转）
User Request (conversationId, input)
  -> AgentRuntimeService.RunAsync
      -> build AgentRunContext (SSOT)
      -> load ShortTermMemory recent_turns
      -> load UserProfile profile
      -> PlannerAgent.CreatePlanAsync(run)
      -> PlanExecutor.ExecuteAsync(run)
           -> foreach step: CreateStepContext (独立输入，不污染 Root)
           -> RouterAgent.ExecuteAsync(stepContext) -> Agent
           -> merge stepContext.State -> ConversationState
      -> commit ShortTermMemory (TurnRecord)
      -> update UserProfile (ProfileExtractor + Upsert)
  -> Response: output + steps + profileSnapshot

## SSOT（AgentRunContext）关键原则
- Root 输入只保存用户原始输入（run.UserInput）
- 每个 Step 使用独立 StepContext
- 会话级共享数据写入 run.ConversationState：
  - recent_turns
  - profile
  - persona（可选）
  - tool/runtime flags

## 关键实现点
### Chat Agent（最优注入方式）
- 使用 Semantic Kernel ChatCompletion + ChatHistory
- persona 通过 history.AddSystemMessage 注入
- profile/recent_turns 从 stepContext.State 注入（由 RuntimeService/Executor 提供）

### Profile（画像）
- Store: IUserProfileStore (InMemory)
- Extractor: ProfileExtractor（规则版优先，后续可升级 LLM 版）
- Response: profileSnapshot 直接回传用于验收

## 验收用例
1. 新会话
- input: "你好，我是笨笨"
- 期望：返回 conversationId；profileSnapshot.name=笨笨（若 extractor 已支持）

2. 画像读取
- input: "你好，我是谁？"
- 期望：优先用 profileSnapshot.name 回答

3. 画像更新
- input: "我在新加坡，晚上11点后才睡"
- 期望：profileSnapshot.location=新加坡；profileSnapshot.sleep=late（按规则）

4. 画像使用
- input: "给我一个适合我的养生建议，最好能坚持"
- 期望：输出考虑新加坡/晚睡背景，且建议可执行

## 已知可优化（不阻塞 Week5）
- Persona 场景分流：寒暄类输入短回复（<=2句+1追问）
- “我是谁/我叫什么” 硬规则：优先使用 profile.name，不得杜撰
- Prompt budget：recent_turns 进一步压缩摘要
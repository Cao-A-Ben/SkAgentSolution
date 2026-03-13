using System;
using System.Collections.Generic;
using System.Text;
using SkAgent.Core.Prompt;
using SkAgent.Runtime.Execution;
using SKAgent.Core.Agent;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Profile;
using SKAgent.Core.Runtime;
using SKAgent.Runtime;
using static SKAgent.Core.Planning.IPlanner;

namespace SkAgent.Runtime.Runtime
{
    /// <summary>
    /// 【Runtime 层 - Agent 运行时服务（总调度器）】
    /// 整个 Agent 系统的顶层编排服务，串联所有环节：
    /// 加载上下文 → 规划 → 执行 → 记忆写入 → 画像更新。
    /// 
    /// 由 AgentController 直接调用，是 API 请求到 Agent 执行的桥梁。
    /// 
    /// 依赖：
    /// - IShortTermMemory：短期记忆存储（读/写对话回合）。
    /// - PlannerAgent：LLM 驱动的计划生成器。
    /// - PlanExecutor：计划执行引擎。
    /// - IUserProfileStore：用户画像存储（读/写画像字段）。
    /// - PersonaOptions：人格配置。
    /// </summary>
    public sealed class AgentRuntimeService
    {
        /// <summary>短期记忆存储接口。</summary>
        private readonly IShortTermMemory _stm;

        /// <summary>计划生成 Agent。</summary>
        private readonly IPlanner _planner;

        /// <summary>计划执行器。</summary>
        private readonly PlanExecutor _executor;

        /// <summary>用户画像存储接口。</summary>
        private readonly IUserProfileStore _profileStore;
        private readonly IProfileExtractor _profileExtractor;
        /// <summary> 已决策快照 </summary>
        private readonly IRunPreparationService _prep;

        /// <summary>人格配置选项。</summary>
        //private readonly PersonaOptions persona;
        /// <summary>
        /// 计划请求工厂，用于从 AgentRunContext 构建 PlannerAgent 所需的 PlanRequest 对象。
        /// </summary>
        private readonly IPlanRequestFactory _planRequestFactory;

        /// <summary>
        /// 初始化运行时服务，注入所有依赖。
        /// </summary>
        public AgentRuntimeService(
            IShortTermMemory stm,
            IPlanner planner,
            PlanExecutor executor,
            IUserProfileStore profileStore,
            IProfileExtractor profileExtractor,
            IRunPreparationService prep,
            IPlanRequestFactory planRequestFactory)
        {
            _stm = stm ?? throw new ArgumentNullException(nameof(stm));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this._profileStore = profileStore;
            this._profileExtractor = profileExtractor;
            //this.persona = persona;

            this._prep = prep;
            this._planRequestFactory = planRequestFactory;
        }

        /// <summary>
        /// 执行一次完整的 Agent 运行流程。
        /// 
        /// 完整流程：
        /// 1. 创建 AgentContext（Root）和 AgentRunContext（SSOT）。
        /// 2. 从 IShortTermMemory 加载最近 N 轮对话记录，写入 ConversationState["recent_turns"]。
        /// 3. 从 IUserProfileStore 加载用户画像，写入 ConversationState["profile"]。
        /// 4. 将 PersonaOptions 写入 ConversationState["persona"]。
        /// 5. 调用 PlannerAgent.CreatPlanAsync 生成执行计划。
        /// 6. 调用 PlanExecutor.ExecuteAsync 逐步执行计划。
        /// 7. 调用 CommitShortTermMemoryAsync 将本轮对话写入短期记忆。
        /// 8. 调用 ProfileExtractor 提取画像字段并通过 UpsertAsync 更新存储。
        /// 9. 返回完整的 AgentRunContext。
        /// </summary>
        /// <param name="conversationId">会话唯一标识 ID。</param>
        /// <param name="input">用户输入文本。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>完整的运行上下文，包含输出、步骤、画像快照等。</returns>
        public async Task<AgentRunContext> RunAsync(string conversationId, string input, IRunEventSink? eventSink = null, CancellationToken ct = default)
        {

            // 1. 创建 Root AgentContext
            var agentContext = new AgentContext
            {
                Input = input,
                ExpectedOutput = string.Empty,
                CancellationToken = ct
            };

            // 2. 创建 AgentRunContext（SSOT），封装本次运行的所有状态
            var run = new AgentRunContext(agentContext, conversationId, eventSink);

            //规划开始
            await run.EmitAsync("run_started", new { input = run.UserInput }, run.Root.CancellationToken);

            // 3. 从短期记忆加载最近 4 轮对话记录，供 Planner 和 ChatAgent 参考上下文
            var recent = await _stm.GetRecentAsync(conversationId, take: 4, ct);
            run.SetRecentTurns(recent);
            run.ConversationState["recent_turns"] = recent;

            // 4) Load profile snapshot
            var profile = await _profileStore.GetAsync(conversationId, ct);
            run.ConversationState["profile"] = profile;
            // 5) PREPARE (persona_selected + memory_layer_included x3)
            await _prep.PrepareAsync(run, ct);

            // 同步到强类型 Prep ; 这些键是 prep service 写入的事实源
            if (run.ConversationState.TryGetValue("persona", out var po) && po is PersonaOptions persona)
                run.Prep.Persona = persona;

            if (run.ConversationState.TryGetValue("memoryBundle", out var mb) && mb is MemoryBundle bundle)
                run.Prep.Memory = bundle;

            // 生成 planner prompt（产出 prompt_composed(target=planner)）
            var plannerPrompt = await _prep.GetPromptAsync(run, PromptTarget.Planner, run.UserInput, 12000, ct);
            // 把 plannerPrompt.User 作为“plannerInput”写进 state，交给 PlanRequestFactory 使用
            run.ConversationState["planner_input"] = plannerPrompt.User;
            run.Prep.PromptCache["planner"] = plannerPrompt; // 可选：缓存

            // 7) Create plan (use factory; no hand-written DTO)
            var planReq = _planRequestFactory.Create(run);
            var plan = await _planner.CreatePlanAsync(planReq);
            //var plan = await _planner.CreatPlanAsync(run);
            run.SetPlan(plan);

            //规划后
            await run.EmitAsync("plan_created", new
            {
                goal = plan.Goal,
                stepCount = plan.Steps.Count,
                steps = plan.Steps.Select(s => new { order = s.Order, kind = s.Kind.ToString(), target = s.Target })
            }, run.Root.CancellationToken);



            // 8. 调用 PlanExecutor 逐步执行计划
            await _executor.ExecuteAsync(run);

            // 9. 将本轮对话记录写入短期记忆
            await CommitShortTermMemoryAsync(run);

            // 10. 使用 ProfileExtractor 从用户输入中提取画像字段
            var patch = _profileExtractor.ExtractPatch(run.UserInput);
            if (patch != null && patch.Count > 0)
            {
                // 10.1 将提取的字段写入画像存储
                await _profileStore.UpsertAsync(conversationId, patch, ct);

                // 10.2 重新加载完整画像并同步到 ConversationState，供 Response 返回 profileSnapshot
                var merged = await _profileStore.GetAsync(conversationId, ct);
                run.ConversationState["profile"] = merged;
            }

            //结束
            //await run.EmitAsync("run_completed", new { finalOutput = run.FinalOutput }, run.Root.CancellationToken);

            return run;
        }


        /// <summary>
        /// 将本轮对话记录写入短期记忆。
        /// 构建 TurnRecord 包含用户输入、助手输出、目标和步骤明细，
        /// 通过 IShortTermMemory.AppendAsync 追加到会话记忆中。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        public async Task CommitShortTermMemoryAsync(AgentRunContext run)
        {
            var conversationId = run.ConversationId;

            // 1. 校验会话 ID
            if (string.IsNullOrWhiteSpace(conversationId)) return;

            // 2. 构建 TurnRecord
            var turnRecord = new TurnRecord
            {
                At = DateTimeOffset.UtcNow,
                UserInput = run.UserInput,
                AssistantOutput = run.FinalOutput ?? string.Empty,
                Goal = run.Goal,
                Steps = [.. run.Steps.Select(s => new StepRecord
                {
                    Order = s.Step.Order,
                    //Agent = s.Step.Agent,
                    Kind = s.Step.Kind.ToString().ToLower(),
                    Target = s.Step.Target,
                    Instruction = s.Step.Instruction,
                    ArgumentsJson = s.Step.ArgumentsJson,
                    Output = s.Output ?? string.Empty,
                    Status = s.Status.ToString()
                })]
            };

            // 3. 追加到短期记忆
            await _stm.AppendAsync(conversationId, turnRecord, run.Root.CancellationToken);
        }

    }
}

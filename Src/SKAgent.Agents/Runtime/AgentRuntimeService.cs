using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Observability;
using SKAgent.Agents.Persona;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Profile;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Runtime
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
        private readonly PlannerAgent _planner;

        /// <summary>计划执行器。</summary>
        private readonly PlanExecutor _executor;

        /// <summary>用户画像存储接口。</summary>
        private readonly IUserProfileStore profileStore;

        /// <summary>人格配置选项。</summary>
        private readonly PersonaOptions persona;

        /// <summary>
        /// 初始化运行时服务，注入所有依赖。
        /// </summary>
        public AgentRuntimeService(
            IShortTermMemory stm,
            PlannerAgent planner,
            PlanExecutor executor,
            IUserProfileStore profileStore,
            PersonaOptions persona)
        {
            _stm = stm ?? throw new ArgumentNullException(nameof(stm));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.profileStore = profileStore;
            this.persona = persona;
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
            var run = new AgentRunContext(agentContext, conversationId);

            //规划开始
            await run.EmitAsync("run_started", new { input = run.UserInput }, run.Root.CancellationToken);

            // 3. 从短期记忆加载最近 4 轮对话记录，供 Planner 和 ChatAgent 参考上下文
            var recent = await _stm.GetRecentAsync(conversationId, take: 4, ct);
            run.SetRecentTurns(recent);

            // 开启调试模式，记录更多细节到 ConversationState
            run.ConversationState["debug_plan"] = true;


            // 4. 将 recent_turns 写入 ConversationState，让 StepContext/ChatAgent 能读取
            run.ConversationState["recent_turns"] = recent;

            // 5. 从画像存储加载用户画像，写入 ConversationState
            var profile = await profileStore.GetAsync(conversationId, ct);
            run.ConversationState["profile"] = profile;

            // 6. 将人格配置写入 ConversationState，Planner 和 ChatAgent 都能使用
            run.ConversationState["persona"] = persona;

            // 7. 调用 PlannerAgent 生成执行计划
            var plan = await _planner.CreatPlanAsync(run);
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
            var patch = ProfileExtractor.ExtractPath(run.UserInput);
            if (patch != null && patch.Count > 0)
            {
                // 10.1 将提取的字段写入画像存储
                await profileStore.UpsertAsync(conversationId, patch, ct);

                // 10.2 重新加载完整画像并同步到 ConversationState，供 Response 返回 profileSnapshot
                var merged = await profileStore.GetAsync(conversationId, ct);
                run.ConversationState["profile"] = merged;
            }

            //结束
            await run.EmitAsync("run_completed", new { finalOutput = run.FinalOutput }, run.Root.CancellationToken);

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

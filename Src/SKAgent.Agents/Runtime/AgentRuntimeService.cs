using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Persona;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Profile;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Runtime
{
    public sealed class AgentRuntimeService
    {

        private readonly IShortTermMemory _stm;
        private readonly PlannerAgent _planner;
        private readonly PlanExecutor _executor;
        private readonly IUserProfileStore profileStore;
        private readonly PersonaOptions persona;

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

        public async Task<AgentRunContext> RunAsync(string conversationId, string input, CancellationToken ct = default)
        {
            var agentContext = new AgentContext
            {
                Input = input,
                ExpectedOutput = string.Empty,
                CancellationToken = ct
            };

            var run = new AgentRunContext(agentContext, conversationId);

            // 读取短期记忆 给planner使用
            var recent = await _stm.GetRecentAsync(conversationId, take: 4, ct);
            run.SetRecentTurns(recent);

            // ✅ 让 StepContext/ChatAgent 能拿到 recent_turns
            run.ConversationState["recent_turns"] = recent;


            var profile = await profileStore.GetAsync(conversationId, ct);
            run.ConversationState["profile"] = profile;

            // ✅ persona 也放到 state，Planner/Chat 都能用
            run.ConversationState["persona"] = persona;

            var plan = await _planner.CreatPlanAsync(run);
            run.SetPlan(plan);

            await _executor.ExecuteAsync(run);

            // Executor 里面已经执行了commit。 在这里执行也可以
            await CommitShortTermMemoryAsync(run);

            // profile 更新(回合结束后写入)
            var patch = ProfileExtractor.ExtractPath(run.UserInput);
            if (patch != null && patch.Count > 0)
            {
                await profileStore.UpsertAsync(conversationId, patch, ct);

                //同步回run 方便 response_snapshot
                var merged = await profileStore.GetAsync(conversationId, ct);
                run.ConversationState["profile"] = merged;
            }


            return run;


        }

        public async Task CommitShortTermMemoryAsync(AgentRunContext run)
        {
            var conversationId = run.ConversationId;
            if (string.IsNullOrWhiteSpace(conversationId)) return;
            var turnRecord = new TurnRecord
            {
                At = DateTimeOffset.UtcNow,
                UserInput = run.UserInput,//用户原始输入
                AssistantOutput = run.FinalOutput ?? string.Empty,
                Goal = run.Goal,
                Steps = run.Steps.Select(s => new StepRecord
                {
                    Order = s.Step.Order,
                    Agent = s.Step.Agent,
                    Instruction = s.Step.Instruction,
                    Output = s.Output ?? string.Empty,
                    Status = s.Status.ToString()
                }).ToList()
            };
            await _stm.AppendAsync(conversationId, turnRecord, run.Root.CancellationToken);
        }

    }
}

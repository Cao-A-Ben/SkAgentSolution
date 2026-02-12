using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Agents.Persona;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;
using SKAgent.Core.Utilities;

namespace SKAgent.Agents.Planning
{
    public class PlannerAgent
    {

        private readonly Kernel _kernel;

        private readonly PersonaOptions _persona;
        public PlannerAgent(Kernel kernel, PersonaOptions persona)
        {
            _kernel = kernel;
            _persona = persona;
        }

        public async Task<AgentPlan> CreatPlanAsync(AgentRunContext run)
        {
            var prompt = """
你是一个 Agent Planner
人格约束[必须遵守]:
{{$hint}}

你的任务是：
- 将用户请求拆解为一组有序的执行步骤(Plan)
- 每个步骤只能由一个Agent执行

你必须严格遵守以下规则：
- 不要使用 Markdown
- 不要输出 ```json
- 不要输出任何解释性文本

可用Agent:
- chat : 适合解释、问答、知识类问题
- mcp  : 适合调用外部系统、工具或协议

输出格式必须是：
{
    "goal":"...",
    "steps":[
        {
           "order":1,
           "agent":"chat",
           "instruction":"...",
           "expectedOutput":"..."
        },
        ...
    ]
}

用户输入:
{{$input}}
""";

            //var result = await _kernel.InvokePromptAsync<PlannerDecision>(prompt, new KernelArguments { ["input"] = userInput });

            var plannerInput = BuildPlannerContext(run);
            OpenAIPromptExecutionSettings settings = new()
            {
                Temperature = 0,
                //ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateTextFormat()
            };
            var arguments = new KernelArguments(settings)
            {
                ["hint"] = _persona.PlannerHint,
                ["input"] = plannerInput,
            };
            var result = await _kernel.InvokePromptAsync(prompt, arguments);



            var raw = result.GetValue<string>()!;
            var json = LlmOutputParser.ExtractJson(raw);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<AgentPlan>(json, options)!;
        }


        private static string BuildPlannerContext(AgentRunContext run)
        {
            //最近turns 只取短摘要，避免prompt膨胀
            var sb = new StringBuilder();
            if (run.ConversationState.TryGetValue("profile", out var p) && p is Dictionary<string, string> profile && profile.Count > 0)
            {
                sb.AppendLine("[User Profile]");
                foreach (var kv in profile)
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                sb.AppendLine();
            }
            if (run.RecentTurns.Count > 0)
            {
                sb.AppendLine("[Recent Conversation Memory]");
                foreach (var t in run.RecentTurns)
                {
                    var u = t.UserInput.Replace("\n", " ").Trim();
                    var a = t.AssistantOutput.Replace("\n", " ").Trim();
                    if (a.Length > 180) a = a[..180] + "...";

                    sb.AppendLine($"- User: {u}");
                    sb.AppendLine($"  Assistant: {a}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("[Current User Input]");
            sb.AppendLine(run.UserInput);

            return sb.ToString();
        }
    }
}

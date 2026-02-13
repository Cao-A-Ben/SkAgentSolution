using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Agents.Persona;
using SKAgent.Agents.Runtime;
using SKAgent.Agents.Tools.Abstractions;
using SKAgent.Core.Agent;
using SKAgent.Core.Utilities;

namespace SKAgent.Agents.Planning
{
    /// <summary>
    /// 【Planning 层 - 计划生成 Agent】
    /// 负责将用户请求通过 LLM 拆解为一组有序的执行步骤（AgentPlan）。
    /// 是整个 Runtime 流程的"大脑"，决定需要哪些 Agent 以什么顺序完成任务。
    /// 
    /// 在运行时流程中的位置：
    /// AgentRuntimeService.RunAsync → PlannerAgent.CreatPlanAsync → PlanExecutor.ExecuteAsync
    /// 
    /// 依赖：
    /// - Kernel：Semantic Kernel 实例，用于调用 LLM。
    /// - PersonaOptions：人格配置，PlannerHint 影响计划拆解策略。
    /// </summary>
    public class PlannerAgent
    {
        /// <summary>Semantic Kernel 实例，用于调用 LLM 生成执行计划。</summary>
        private readonly Kernel _kernel;

        /// <summary>人格配置，提供 PlannerHint 作为计划拆解的额外约束。</summary>
        private readonly PersonaOptions _persona;

        /// <summary>工具注册表，提供可用工具列表以注入 Planner prompt。</summary>
        private readonly IToolRegistry _toolRegistry;

        /// <summary>
        /// 初始化 PlannerAgent。
        /// </summary>
        /// <param name="kernel">Semantic Kernel 实例。</param>
        /// <param name="persona">人格配置选项，提供 PlannerHint。</param>
        /// <param name="toolRegistry">工具注册表，提供可用工具目录。</param>
        public PlannerAgent(Kernel kernel, PersonaOptions persona, IToolRegistry toolRegistry)
        {
            _kernel = kernel;
            _persona = persona;
            _toolRegistry = toolRegistry;
        }

        /// <summary>
        /// 调用 LLM 将用户请求拆解为执行计划（AgentPlan）。
        /// 
        /// 流程：
        /// 1. 构建 Planner 上下文（包含 Profile、RecentTurns、当前输入）。
        /// 2. 将 PersonaOptions.PlannerHint 注入 prompt 模板的 {{$hint}} 变量。
        /// 3. 调用 Semantic Kernel InvokePromptAsync 请求 LLM 生成 JSON 格式的计划。
        /// 4. 使用 LlmOutputParser 从原始输出中提取 JSON。
        /// 5. 反序列化为 AgentPlan 对象返回。
        /// </summary>
        /// <param name="run">当前运行上下文，包含用户输入、Profile、RecentTurns 等信息。</param>
        /// <returns>LLM 生成的执行计划。</returns>
        public async Task<AgentPlan> CreatPlanAsync(AgentRunContext run)
        {

            if (run.ConversationState.TryGetValue("debug_plan", out var v) && v is true)
            {
                return new AgentPlan
                {
                    Goal = "debug tools",
                    Steps = new List<PlanStep>{
            new PlanStep{ Order=1, Kind=PlanStepKind.Tool, Target="string.upper", ArgumentsJson="{\"text\":\"hello\"}" },
            new PlanStep{ Order=2, Kind=PlanStepKind.Agent, Target="chat", Instruction="解释上一步工具输出" }
                    }
                };
            }


            // Planner 的 prompt 模板，指导 LLM 输出 JSON 格式的执行计划
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
可用Tools:
{{$tools}}

输出格式必须是：
{
    "goal":"...",
    "steps":[
        {
           "order":1,
           "kind":"tool",
           "target":"string.upper",
           "argumentsJson": "{\"text\":\"hello\"}",
           "expectedOutput": "HELLO"
        },
         {
           "order": 2,
           "kind": "agent",
           "target": "chat",
           "instruction": "解释上一步结果给用户",
           "expectedOutput": "用户理解含义"
         }
        ...
    ]
}

字段规则:
- Kind 只能是"agent"或 "tool"
- kind="agent"时, 必须有target、instruction; argumentsJson 必须为空或缺省值
- kind="tool"时, 必须有target、argumentsJson; instruction 可为空或缺省值
- argumentsJson必须是一个JSON字符串(注意转义)，且能反序列化为一个对象,不要输出嵌套对象

用户输入:
{{$input}}
""";

            // 1. 构建 Planner 输入上下文（Profile + RecentTurns + 当前输入）
            var plannerInput = BuildPlannerContext(run);

            // 2. 配置 LLM 参数：Temperature=0 确保输出稳定可预测
            OpenAIPromptExecutionSettings settings = new()
            {
                Temperature = 0,
            };

            // 3. 将 hint 和 input 注入 prompt 模板变量
            var arguments = new KernelArguments(settings)
            {
                ["tools"] = BuildToolCatalog(),
                ["hint"] = _persona.PlannerHint,
                ["input"] = plannerInput,
            };

            // 4. 调用 Semantic Kernel 执行 prompt，获取 LLM 原始输出
            var result = await _kernel.InvokePromptAsync(prompt, arguments);



            // 5. 从 LLM 原始输出中提取 JSON 字符串（去除可能的 Markdown 包裹）
            var raw = result.GetValue<string>()!;
            var json = LlmOutputParser.ExtractJson(raw);

            // 6. 反序列化为 AgentPlan 对象（属性名忽略大小写）
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };

            return JsonSerializer.Deserialize<AgentPlan>(json, options)!;
        }


        /// <summary>
        /// 构建 Planner 的输入上下文字符串，包含 Profile、最近对话记忆和当前用户输入。
        /// 确保 Planner 能基于足够的上下文生成合理的执行计划。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        /// <returns>拼接后的 Planner 输入文本。</returns>
        private static string BuildPlannerContext(AgentRunContext run)
        {
            var sb = new StringBuilder();

            // 1. 如果有用户画像，拼接 [User Profile] 段
            if (run.ConversationState.TryGetValue("profile", out var p) && p is Dictionary<string, string> profile && profile.Count > 0)
            {
                sb.AppendLine("[User Profile]");
                foreach (var kv in profile)
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                sb.AppendLine();
            }

            // 2. 如果有最近对话记忆，拼接 [Recent Conversation Memory] 段（摘要形式，避免 prompt 膨胀）
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

            // 3. 拼接当前用户输入
            sb.AppendLine("[Current User Input]");
            sb.AppendLine(run.UserInput);

            return sb.ToString();
        }


        /// <summary>
        /// 构建可用工具目录字符串，注入 Planner prompt 的 {{$tools}} 变量。
        /// 格式：每行 "- 工具名: 描述"，无可用工具时返回 "(无)"。
        /// </summary>
        private string BuildToolCatalog()
        {
            var tools = _toolRegistry.List();
            if (tools.Count == 0) return "(无)";

            var sb = new StringBuilder();
            foreach (var tool in tools)
            {
                sb.AppendLine($"- {tool.Name}: {tool.Description}");
            }
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkAgent.Core.Prompt;
using SKAgent.Application.Prompt;
using SKAgent.Application.Runtime;
using SKAgent.Core.Agent;
using SKAgent.Core.Memory;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Core.Utilities;
using static SKAgent.Core.Planning.IPlanner;

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
    public class PlannerAgent : IPlanner
    {
        /// <summary>Semantic Kernel 实例，用于调用 LLM 生成执行计划。</summary>
        private readonly Kernel _kernel;

        /// <summary>人格配置，提供 PlannerHint 作为计划拆解的额外约束。</summary>
        //private readonly PersonaOptions _persona; //改为_promptComposer

        //private readonly PromptComposer _promptComposer;

        /// <summary>工具注册表，提供可用工具列表以注入 Planner prompt。</summary>
        private readonly IToolRegistry _toolRegistry;

        /// <summary>
        /// 初始化 PlannerAgent。
        /// </summary>
        /// <param name="kernel">Semantic Kernel 实例。</param>
        /// <param name="persona">人格配置选项，提供 PlannerHint。</param>PersonaOptions persona,
        /// <param name="toolRegistry">工具注册表，提供可用工具目录。</param>
        ///// <param name="promptComposer">Prompt 组合器，用于生成 Planner prompt。</param>
        public PlannerAgent(Kernel kernel, IToolRegistry toolRegistry)
        {
            _kernel = kernel;
            //_promptComposer = promptComposer;
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
        public async Task<AgentPlan> CreatePlanAsync(IPlanner.PlanRequest req)
        {

            if (req.DebugPlan)
            {
                return new AgentPlan
                {
                    Goal = "debug tools",
                    Steps = new List<PlanStep>
                {
                    new PlanStep { Order = 1, Kind = PlanStepKind.Tool, Target = "string.upper", ArgumentsJson = "{\"text\":\"hello\"}" },
                    new PlanStep { Order = 2, Kind = PlanStepKind.Agent, Target = "chat", Instruction = "解释上一步工具输出" }
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
工具使用硬规则（必须遵守）：
- 如果某个步骤可以通过可用Tools直接完成（如字符串处理、时间获取、计算、HTTP请求），必须优先生成 kind="tool" 的步骤。
- kind="tool" 时：
  - target 必须严格从 TOOLS CATALOG 的 name 中选择，禁止杜撰。
  - argumentsJson 必须符合该工具的 input schema。
  - argumentsJson 必须是“JSON字符串”（带引号、内部双引号要转义），不要输出嵌套对象。
- kind="agent" 时：
  - 仅用于解释、总结、提出澄清问题或当工具无法完成时。

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
- kind 只能是"agent"或 "tool"
- kind="agent"时, 必须有target、instruction; argumentsJson 必须为空或缺省值
- kind="tool"时, 必须有target、argumentsJson; instruction 可为空或缺省值
- argumentsJson必须是一个JSON字符串(注意转义)，且能反序列化为一个对象,不要输出嵌套对象

用户输入:
{{$input}}
""";

            // 1. 构建 Planner 输入上下文（Profile + RecentTurns + 当前输入）
            //var plannerInput = BuildPlannerContext(run);

            //var task = run.UserInput;

            //var composed = _promptComposer.Compose(
            //                    run,
            //                    persona,
            //                    bundle,
            //                    PromptTarget.Planner,
            //                    taskOrUserMessage: task,
            //                    charBudget: 12000,
            //                    ct: run.Root.CancellationToken);

            // 2. 配置 LLM 参数：Temperature=0 确保输出稳定可预测
            OpenAIPromptExecutionSettings settings = new()
            {
                Temperature = 0,
            };

            // 3. 将 hint 和 input 注入 prompt 模板变量
            var arguments = new KernelArguments(settings)
            {
                ["tools"] = BuildToolCatalog(),
                //["hint"] = _persona.PlannerHint,
                //["input"] = plannerInput,
                ["hint"] = req.PlannerHint,     // 动态 persona
                ["input"] = req.UserInput,          // 统一入口生成的 plannerInput
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


        private string BuildToolCatalog()
        {
            var tools = _toolRegistry.List();
            if (tools.Count == 0) return "(无)";

            var sb = new StringBuilder();
            sb.AppendLine("TOOLS CATALOG (choose target strictly from this list):");

            foreach (var t in tools)
            {
                sb.AppendLine($"- name: {t.Name}");
                sb.AppendLine($"  desc: {t.Description}");

                // Input schema summary
                var props = t.InputSchema.Properties ?? new Dictionary<string, ToolFieldSchema>();
                var required = t.InputSchema.Required ?? Array.Empty<string>();

                if (props.Count == 0)
                {
                    sb.AppendLine("  input: {}");
                    sb.AppendLine("  example_argumentsJson: \"{}\"");
                }
                else
                {
                    sb.AppendLine("  input:");
                    foreach (var kv in props)
                    {
                        var req = required.Contains(kv.Key) ? " (required)" : "";
                        sb.AppendLine($"    - {kv.Key}: {kv.Value.Type}{req} // {kv.Value.Description}");
                    }

                    // Generate a simple example json string (best effort)
                    var exampleObj = new Dictionary<string, object?>();
                    foreach (var kv in props)
                    {
                        exampleObj[kv.Key] = kv.Value.Type switch
                        {
                            "string" => "example",
                            "integer" => 1,
                            "number" => 1.0,
                            "boolean" => true,
                            _ => null
                        };
                    }

                    var exampleJson = JsonSerializer.Serialize(exampleObj);
                    // 注意：argumentsJson 本身是一个 JSON 字符串，所以外层要加引号；这里输出时直接给转义后的字符串样式
                    sb.AppendLine($"  example_argumentsJson: \"{exampleJson.Replace("\"", "\\\"")}\"");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

    }
}

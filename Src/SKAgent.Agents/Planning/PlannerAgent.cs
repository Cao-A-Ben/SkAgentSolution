using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Core.Utilities;

namespace SKAgent.Agents.Planning
{
    public class PlannerAgent
    {

        private readonly Kernel _kernel;

        public PlannerAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<AgentPlan> CreatPlanAsync(string userInput)
        {
            var prompt = """
你是一个 Agent Planner
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


            OpenAIPromptExecutionSettings settings = new()
            {
                Temperature = 0,
                //ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateTextFormat()
            };
            var arguments = new KernelArguments { ["input"] = userInput };
            var result = await _kernel.InvokePromptAsync(prompt, arguments);



            var raw = result.GetValue<string>()!;
            var json = LlmOutputParser.ExtractJson(raw);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<AgentPlan>(json, options)!;
        }


    }
}

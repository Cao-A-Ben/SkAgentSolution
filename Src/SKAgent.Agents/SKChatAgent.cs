using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Agents.Chat;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    /// <summary>
    /// 【Agents 层 - Semantic Kernel Chat Agent】
    /// 基于 Semantic Kernel 的对话 Agent，是当前系统的核心回复引擎。
    /// 适用于解释、问答、知识类问题（对应 PlanStep.Agent = "chat"）。
    /// 
    /// 工作流程：
    /// 1. 通过 IChatContextComposer 将 StepContext 组合为 ChatContext（SystemMessage + UserMessage）。
    /// 2. 构建 Semantic Kernel ChatHistory，注入系统消息和用户消息。
    /// 3. 调用 IChatCompletionService 获取 LLM 回复。
    /// 4. 封装为 AgentResult 返回。
    /// </summary>
    public class SKChatAgent : IAgent
    {
        /// <summary>Semantic Kernel 实例，用于获取 IChatCompletionService。</summary>
        private readonly Kernel _kernel;

        /// <summary>对话上下文组合器，负责拼接 Persona/Profile/Memory 为 Prompt。</summary>
        private readonly IChatContextComposer _composer;

        /// <summary>Agent 名称，用于 RouterAgent 路由匹配。</summary>
        public string Name => "chat";

        /// <summary>
        /// 初始化 SKChatAgent。
        /// </summary>
        /// <param name="kernel">Semantic Kernel 实例。</param>
        /// <param name="composer">对话上下文组合器。</param>
        public SKChatAgent(Kernel kernel, IChatContextComposer composer)
        {
            _kernel = kernel;
            _composer = composer;
        }

        /// <summary>
        /// 执行对话 Agent 的核心逻辑。
        /// </summary>
        /// <param name="context">当前步骤的执行上下文（StepContext）。</param>
        /// <returns>包含 LLM 回复文本的 AgentResult。</returns>
        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            // 1. 从 Kernel 获取 Chat Completion 服务
            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            // 2. 通过 Composer 将 StepContext 组合为 ChatContext
            var composed = _composer.Compose(context);

            // 3. 构建 ChatHistory，注入系统消息和用户消息
            var history = new ChatHistory();
            history.AddSystemMessage(composed.SystemMessage);
            history.AddUserMessage(composed.UserMessage);

            // 4. 配置 LLM 参数：Temperature=0.3 保持较稳定的输出，TopP=0.9
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3,
                TopP = 0.9
            };

            // 5. 调用 LLM 获取回复
            var msg = await chat.GetChatMessageContentAsync(
                history,
                executionSettings: settings,
                kernel: _kernel,
                context.CancellationToken);

            // 6. 封装为 AgentResult 返回
            return new AgentResult
            {
                Output = msg.Content ?? string.Empty,
                IsSuccess = true
            };
        }
    }
}

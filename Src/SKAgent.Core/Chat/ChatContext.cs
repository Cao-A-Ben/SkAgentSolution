using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Chat
{
    /// <summary>
    /// 【Chat 层 - 对话上下文数据对象】
    /// 封装传递给 Semantic Kernel ChatHistory 的系统消息和用户消息。
    /// 由 IChatContextComposer.Compose 构建，由 SKChatAgent.ExecuteAsync 消费。
    /// </summary>
    public sealed class ChatContext
    {
        /// <summary>
        /// 系统消息（System Prompt），包含人格描述、用户画像、回复策略等。
        /// 注入到 ChatHistory.AddSystemMessage 中。
        /// </summary>
        public string SystemMessage { get; init; } = string.Empty;

        /// <summary>
        /// 用户消息，包含最近对话记忆摘要和当前任务指令。
        /// 注入到 ChatHistory.AddUserMessage 中。
        /// </summary>
        public string UserMessage { get; init; } = string.Empty;
    }
}

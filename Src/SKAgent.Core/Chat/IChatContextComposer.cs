using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Agent;

namespace SKAgent.Core.Chat
{
    /// <summary>
    /// 【Chat 层 - 对话上下文组合器接口】
    /// 定义将 AgentContext（StepContext）转换为 ChatContext（SystemMessage + UserMessage）的契约。
    /// SKChatAgent 通过此接口获取构建好的对话上下文，再传递给 Semantic Kernel ChatCompletion。
    /// </summary>
    public interface IChatContextComposer
    {
        /// <summary>
        /// 根据步骤上下文组合对话所需的系统消息和用户消息。
        /// </summary>
        /// <param name="stepContext">当前步骤的执行上下文，包含 Input 和 State。</param>
        /// <returns>组合后的 ChatContext，包含 SystemMessage 和 UserMessage。</returns>
        ChatContext Compose(AgentContext stepContext);
    }
}

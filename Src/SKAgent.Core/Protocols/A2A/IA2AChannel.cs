using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.A2A
{
    /// <summary>
    /// 【Core 协议层 - A2A（Agent-to-Agent）通信通道接口】
    /// 定义 Agent 间点对点通信的抽象契约，支持跨进程/跨服务的 Agent 调用。
    /// 基础设施层通过 LocalA2AChannel 提供本地模拟实现。
    /// 当前版本用于协议扩展预留，暂未在主流程中调用。
    /// </summary>
    public interface IA2AChannel
    {
        /// <summary>
        /// 异步发送 A2A 消息到目标 Agent 并获取响应。
        /// </summary>
        /// <param name="message">A2A 消息，包含目标 Agent 名称和 Payload 内容。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>目标 Agent 的响应文本。</returns>
        Task<string> SendAsync(A2AMessage message, CancellationToken cancellationToken = default);
    }
}

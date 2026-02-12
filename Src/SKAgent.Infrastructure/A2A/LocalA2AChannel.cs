using System;
using System.Collections.Generic;
using System.Text;

using SKAgent.Core.Protocols.A2A;

namespace SKAgent.Infrastructure.A2A
{
    /// <summary>
    /// 【基础设施层 - A2A 本地通道实现（模拟版）】
    /// IA2AChannel 的本地模拟实现，当前仅返回模拟响应。
    /// 用于协议扩展预留，暂未在主流程中调用。
    /// 后续可替换为基于 HTTP/gRPC 的跨服务 Agent 通信实现。
    /// </summary>
    public class LocalA2AChannel : IA2AChannel
    {
        /// <summary>
        /// 模拟发送 A2A 消息，直接返回模拟响应。
        /// </summary>
        public Task<string> SendAsync(A2AMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"[A2A:{message.Agent}] {message.Payload}");
        }
    }
}

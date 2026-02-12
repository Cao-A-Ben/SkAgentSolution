using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.A2A
{
    /// <summary>
    /// 【Core 协议层 - A2A 消息记录】
    /// Agent-to-Agent 通信的消息载体，使用 record 类型保证不可变性。
    /// 当前版本用于协议扩展预留，暂未在主流程中调用。
    /// </summary>
    /// <param name="Agent">目标 Agent 名称。</param>
    /// <param name="Payload">消息内容（通常为 JSON 或纯文本）。</param>
    public sealed record A2AMessage(string Agent, string Payload);
}

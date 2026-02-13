using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具统一接口】
    /// 所有可注册到 ToolRegistry 的工具必须实现此接口。
    /// 提供工具元数据描述（Descriptor）和异步调用入口（InvokeAsync）。
    /// 适配器层（FunctionTool / HttpTool）均实现此接口。
    /// </summary>
    public interface ITool
    {
        /// <summary>工具元数据描述，包含名称、输入/输出 Schema、超时等信息。</summary>
        ToolDescriptor Descriptor { get; }

        /// <summary>
        /// 执行工具调用。
        /// </summary>
        /// <param name="args">调用参数，以 JSON 结构传入。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>工具执行结果，包含成功/失败、输出和性能指标。</returns>
        Task<ToolResult> InvokeAsync(JsonElement args, CancellationToken ct);
    }
}

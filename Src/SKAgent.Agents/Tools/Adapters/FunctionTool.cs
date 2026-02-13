using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Tools.Abstractions;

namespace SKAgent.Agents.Tools.Adapters
{
    /// <summary>
    /// 【Tools 适配器层 - 委托函数工具】
    /// 将任意 Func&lt;JsonElement, CancellationToken, Task&lt;JsonElement&gt;&gt; 委托包装为 ITool 实现。
    /// 适用于快速注册轻量级工具（如字符串转换、数学计算等），无需单独定义类。
    /// </summary>
    public sealed class FunctionTool : ITool
    {
        /// <summary>实际执行的委托函数。</summary>
        private readonly Func<JsonElement, CancellationToken, Task<JsonElement>> _fn;

        /// <inheritdoc />
        public ToolDescriptor Descriptor { get; }

        /// <summary>
        /// 初始化委托函数工具。
        /// </summary>
        /// <param name="descriptor">工具元数据描述。</param>
        /// <param name="fn">实际执行的异步委托，接受 JSON 参数并返回 JSON 结果。</param>
        public FunctionTool(ToolDescriptor descriptor, Func<JsonElement, CancellationToken, Task<JsonElement>> fn)
        {
            Descriptor = descriptor;
            _fn = fn;
        }

        /// <inheritdoc />
        public async Task<ToolResult> InvokeAsync(JsonElement args, CancellationToken ct)
        {
            var output = await _fn(args, ct);
            return new ToolResult(true, output);
        }
    }
}

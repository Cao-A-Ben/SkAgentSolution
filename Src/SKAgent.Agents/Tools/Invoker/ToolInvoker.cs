using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Tools.Abstractions;

namespace SKAgent.Agents.Tools.Invoker
{
    /// <summary>
    /// 【Tools 调用器层 - 工具调用器实现】
    /// IToolInvoker 的默认实现，负责：
    /// 1. 从 IToolRegistry 按名称查找工具实例。
    /// 2. 执行工具并自动测量耗时（Stopwatch）。
    /// 3. 处理超时（基于 ToolDescriptor.TimeoutMs）和异常，统一返回 ToolResult。
    /// 
    /// 在运行时流程中的位置：
    /// PlanExecutor (Kind=Tool) → ToolInvoker.InvokeAsync → ITool.InvokeAsync
    /// </summary>
    public sealed class ToolInvoker : IToolInvoker
    {
        /// <summary>工具注册表，用于按名称查找工具实例。</summary>
        private readonly IToolRegistry _registry;

        /// <summary>预分配的空 JSON 对象，避免失败时重复解析。</summary>
        private static readonly JsonElement EmptyJson =
    JsonDocument.Parse("{}").RootElement.Clone();

        /// <summary>
        /// 初始化工具调用器。
        /// </summary>
        /// <param name="registry">工具注册表实例。</param>
        public ToolInvoker(IToolRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 根据调用描述执行工具。
        /// 流程：查找工具 → 启动计时 → 执行（含超时控制）→ 补充 Metrics → 返回结果。
        /// </summary>
        /// <inheritdoc />
        public async Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct)
        {
            if (!_registry.TryGet(invocation.ToolName, out var tool))
            {

                return Fail("tool_not_found", $"Tool '{invocation.ToolName}' not found.", 0);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                var timeoutMs = tool.Descriptor.TimeoutMs;
                if (timeoutMs is null)
                {
                    var r = await tool.InvokeAsync(invocation.Arguments, ct);
                    return r with { Metrics = (r.Metrics ?? new ToolMetrics(sw.ElapsedMilliseconds)) };
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs.Value);

                var result = await tool.InvokeAsync(invocation.Arguments, cts.Token);
                return result with { Metrics = (result.Metrics ?? new ToolMetrics(sw.ElapsedMilliseconds)) };
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Fail("tool_timeout", $"Tool timeout: {invocation.ToolName}", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return Fail("tool_exception", ex.Message, sw.ElapsedMilliseconds, ex.GetType().FullName);
            }
        }



        /// <summary>
        /// 构建失败的 ToolResult，使用预分配的 EmptyJson 避免重复解析。
        /// </summary>
        private static ToolResult Fail(string code, string message, long latencMs, object? details = null)
        {

            //有资源/性能问题
            //var output = JsonDocument.Parse("{}").RootElement;

            return new ToolResult
            (
                Success: false,
                Output: EmptyJson,
                Error: new ToolError(code, message, details),
                Metrics: new ToolMetrics(latencMs)
            );

        }
    }
}

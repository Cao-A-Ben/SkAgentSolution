using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Tools
{
    /// <summary>
    /// 【Core 抽象层 - Agent 工具接口】
    /// 定义 Agent 可调用的外部工具的统一契约。
    /// 用于后续扩展 Agent 的工具调用能力（如搜索、数据库查询、文件操作等）。
    /// 当前版本尚未在具体 Agent 中集成，保留供后续 Week 迭代使用。
    /// </summary>
    public interface IAgentTool
    {
        /// <summary>
        /// 工具的唯一名称标识，用于在 Agent 工具集中注册和查找。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 异步执行工具逻辑，接收文本输入并返回文本输出。
        /// </summary>
        /// <param name="input">工具调用的输入参数（通常为 JSON 或纯文本）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>工具执行的文本结果。</returns>
        Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default);
    }
}

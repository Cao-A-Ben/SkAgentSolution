using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 运行期工作记忆，保存最近一步与工具调用的摘要。
    /// 用于在后续步骤中提供上下文提示，避免 prompt 过大。
    /// </summary>
    public sealed class RunWorkingMemory
    {
        /// <summary>
        /// 最近一步的执行摘要（StepSnapshot）。
        /// </summary>
        public StepSnapshot? LastStep { get; set; }

        /// <summary>
        /// 最近一次工具调用的摘要（ToolSnapshot）。
        /// </summary>
        public ToolSnapshot? LastTool { get; set; }

        // 便于后续步骤引用：order -> snapshot（只存摘要，防 prompt 爆炸）
        public Dictionary<int, StepSnapshot> Steps { get; } = new Dictionary<int, StepSnapshot>();
    }

    /// <summary>
    /// 步骤执行的摘要快照，用于工作记忆与快速回顾。
    /// </summary>
    public sealed record StepSnapshot(
        int Order,
        string Kind, // "Agent" | "Tool" | "Final"
        string Target, // AgentName | ToolName | "output"
        string? OutputPreview, //截断后的输出预览，避免大文本占用过多空间
        bool Success,
        string? Error
        );


    /// <summary>
    /// 工具调用的摘要快照，用于工作记忆与可观测性对齐。
    /// </summary>
    public sealed record ToolSnapshot(
        int Order,
        string ToolName,
        string? ArgsPreview,
        string? OutputPreview,
        bool Success,
        string? ErrorCode,
        string? ErrorMessage,
        long LatencyMs
        );
}

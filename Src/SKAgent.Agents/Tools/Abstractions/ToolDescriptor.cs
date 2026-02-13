using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具元数据描述】
    /// 定义单个工具的完整协议信息，包括名称、说明、输入/输出 Schema、标签和超时策略。
    /// 由 ITool.Descriptor 暴露，PlannerAgent 注入 prompt 时使用 Name + Description，
    /// ToolInvoker 执行时使用 TimeoutMs 控制超时。
    /// </summary>
    /// <param name="Name">工具唯一名称，对应 PlanStep.Target（Kind=Tool 时）。</param>
    /// <param name="Description">工具功能描述，注入 Planner prompt 供 LLM 选择工具。</param>
    /// <param name="InputSchema">输入参数 Schema，定义工具接受的 JSON 结构。</param>
    /// <param name="OutputSchema">输出参数 Schema（可选），用于结果校验。</param>
    /// <param name="Tags">分类标签（可选），便于按类别筛选工具。</param>
    /// <param name="TimeoutMs">执行超时毫秒数（可选），ToolInvoker 据此设置 CancelAfter。</param>
    /// <param name="Idempotent">是否幂等（可选），预留给重试策略使用。</param>
    public sealed record ToolDescriptor(
        string Name,
        string Description,
        ToolParameterSchema InputSchema,
        ToolParameterSchema? OutputSchema = null,
        IReadOnlyList<string>? Tags = null,
        int? TimeoutMs = null,
        bool? Idempotent = null
        );
}

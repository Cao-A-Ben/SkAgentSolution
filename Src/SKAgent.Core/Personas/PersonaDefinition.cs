using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Personas
{
    /// <summary>
    /// 人格定义模型（可由 JSON 配置加载）。
    /// </summary>
    public sealed record PersonaDefinition(
     string Id,
     string DisplayName,
     string SystemPrompt,
     string? PlannerHint = null,
     PersonaPolicy? Policy = null
 );

    /// <summary>
    /// 工具访问策略（白名单/黑名单）。
    /// </summary>
    public sealed record ToolPolicy(
        IReadOnlySet<string>? AllowTools = null,
        IReadOnlySet<string>? BlockTools = null
    );

    /// <summary>
    /// 记忆预算策略。
    /// </summary>
    public sealed record MemoryPolicy(
        int ShortTermBudgetChars = 4000,
        int WorkingBudgetChars = 4000,
        int LongTermBudgetChars = 4000
    );

    /// <summary>
    /// 文本风格策略。
    /// </summary>
    public sealed record StylePolicy(
        string? Tone = null,
        string? Language = null
    );

}

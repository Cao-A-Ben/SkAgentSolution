using System;
using System.Collections.Generic;
using System.Text;
namespace SkAgent.Core.Prompt;

/// <summary>
/// Prompt 目标类型。
/// </summary>
public enum PromptTarget
{
    Planner = 1,
    Chat = 2,
    Reflection = 3
}

/// <summary>
/// Prompt 组合结果。
/// 记录了目标、system/user 文本、哈希、预算与使用到的记忆层，便于审计与观测。
/// </summary>
public sealed record ComposedPrompt(
    PromptTarget Target,
    string System,
    string User,
    string Hash,
    int CharBudget,
    IReadOnlyList<string> LayersUsed
);


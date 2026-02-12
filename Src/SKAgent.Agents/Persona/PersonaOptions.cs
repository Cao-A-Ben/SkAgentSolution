using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualBasic;

namespace SKAgent.Agents.Persona
{
    /// <summary>
    /// 【Persona 层 - 人格配置选项】
    /// 系统级人格约束的配置类，影响 ChatAgent 和 PlannerAgent 的行为风格。
    /// 通过 DI 容器以单例注册，当前默认使用 PersonaCatalog.EngineerTCM。
    /// 
    /// 使用场景：
    /// - ChatContextComposer 使用 SystemPrompt 构建 ChatHistory 的系统消息。
    /// - PlannerAgent 使用 PlannerHint 作为计划拆解的额外约束指引。
    /// </summary>
    public sealed class PersonaOptions
    {
        /// <summary>
        /// 人格配置的唯一名称标识，用于区分不同的人格预设。
        /// 例如: "engineer_tcm"、"neutral"。
        /// </summary>
        public string Name { get; init; } = "default";

        /// <summary>
        /// 系统提示词（System Prompt），注入到 ChatHistory 的 SystemMessage 中。
        /// 定义 AI 助手的核心角色、回复规则和输出风格。
        /// 由 ChatContextComposer.BuildSystem 读取并拼接到系统消息中。
        /// </summary>
        public string SystemPrompt { get; init; } = string.Empty;

        /// <summary>
        /// 可选的 Planner 指引提示，影响 PlannerAgent 如何拆解执行计划。
        /// 例如: "规划时优先选择最少步骤达成目标"。
        /// 由 PlannerAgent.CreatPlanAsync 中的 prompt 模板通过 {{$hint}} 变量注入。
        /// </summary>
        public string PlannerHint { get; init; } = string.Empty;
    }
}

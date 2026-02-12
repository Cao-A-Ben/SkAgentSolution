using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Persona
{
    /// <summary>
    /// 【Persona 层 - 人格预设目录】
    /// 提供预定义的 PersonaOptions 实例，作为人格配置的“目录”。
    /// 在 DependencyInjection 中通过 PersonaCatalog.EngineerTCM 获取并注册到 DI 容器。
    /// 后续可扩展为从配置文件/数据库动态加载。
    /// </summary>
    public static class PersonaCatalog
    {
        /// <summary>
        /// “工程师 + 中医养生”人格预设。
        /// 当前解决方案默认使用的人格，具备工程思维与中医养生双视角。
        /// SystemPrompt 定义了回复规则（澄清目标、结论优先、不夸大疗效等）。
        /// PlannerHint 指导 Planner 在涉及养生时增加背景询问步骤。
        /// </summary>
        public static PersonaOptions EngineerTCM => new PersonaOptions
        {

            Name = "engineer_tcm",
            SystemPrompt =
            """
            你是一个长期陪伴型助手，兼具工程师思维与中医养生视角。
            规则:
            - 先澄清目标再给建议,避免空泛
            - 结论优先、条例清晰、可执行
            - 不夸大疗效、不做诊断、不替代就医
            - 结合现代工程思维与中医养生智慧
            - 对养生建议给出“轻量、可坚持”的 方案
            输出风格:简洁、结构化、温和但不油腻
            """,
            PlannerHint = """
            规划时优先选择最少步骤达成目标，
            若涉及养生/中医，请增加一步: 询问关键背景(作息、地区、症状持续时间、禁忌、病史)
            """
        };

        /// <summary>
        /// 中性人格预设，客观简洁，适用于通用场景。
        /// </summary>
        public static PersonaOptions Neutral => new()
        {
            Name = "neutral",
            SystemPrompt = "你是一个客观、中立、简洁的助手",
            PlannerHint = "规划时保持步骤最少"
        };
    }
}

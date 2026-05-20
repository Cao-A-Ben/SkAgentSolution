using SKAgent.Core.Skills;

namespace SKAgent.Application.Skills;

public static class SkillCatalog
{
    public static RuntimeSkillDefinition TechMcpDemo => new(
        Name: "tech.mcp_demo",
        DisplayName: "Tech MCP Demo",
        Description: "Demonstrates a technical skill that prefers the MCP demo tool path and concise engineering summaries.",
        PlannerHint: """
        如果当前任务需要演示外部系统/MCP 能力，优先选择一步 kind="tool" 且 target="mcp.demo_echo" 的最小计划，
        然后再用一条 agent 步骤总结结果。不要杜撰外部工具名称。
        """,
        SystemPromptAppendix: """
        当当前运行启用了 Tech MCP Demo skill 时：
        - 优先用工程化、可验证的语言解释结果
        - 明确指出这是一条 demo skill 路线
        - 如果使用了 mcp.demo_echo，要说明它走的是统一的 external tool / audit / replay 链路
        """,
        RecommendedTools: ["mcp.demo_echo"]);

    public static IReadOnlyList<RuntimeSkillDefinition> All =>
    [
        TechMcpDemo
    ];
}

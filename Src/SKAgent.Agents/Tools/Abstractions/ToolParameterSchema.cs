using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具参数 Schema】
    /// 描述工具输入/输出的 JSON 结构定义，嵌套在 ToolDescriptor 中。
    /// 用于 Planner prompt 注入和后续参数校验。
    /// </summary>
    /// <param name="Type">根类型，默认为 "object"，可为 "string"、"number"、"boolean"、"array"、"file"。</param>
    /// <param name="Properties">字段定义字典，key 为字段名，value 为字段 Schema。</param>
    /// <param name="Required">必填字段名列表（可选）。</param>
    public sealed record ToolParameterSchema(
     string Type,
     IReadOnlyDictionary<string, ToolFieldSchema> Properties,
     IReadOnlyList<string>? Required = null
    );


    /// <summary>
    /// 【Tools 抽象层 - 单个字段 Schema】
    /// 描述 ToolParameterSchema 中某个字段的类型和说明。
    /// </summary>
    /// <param name="Type">字段类型，如 "string"、"number"、"boolean"、"array"、"file"、"object"。</param>
    /// <param name="Description">字段说明（可选）。</param>
    public sealed record ToolFieldSchema(
     string Type,
     string? Description = null
    );
}

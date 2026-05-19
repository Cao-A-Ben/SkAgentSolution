using System;
using System.Collections.Generic;
using System.Linq;

namespace SKAgent.Core.Tools.Abstractions;

public sealed record ToolAccessDecision(
    bool IsExternal,
    bool Allowed,
    bool VisibleToPlanner,
    bool RequiresAudit,
    string? Reason = null);

public interface IToolAccessPolicy
{
    ToolAccessDecision Evaluate(ToolDescriptor descriptor);
}

public sealed class AllowAllToolAccessPolicy : IToolAccessPolicy
{
    public ToolAccessDecision Evaluate(ToolDescriptor descriptor)
    {
        var isExternal = descriptor.Tags?.Any(static tag =>
            string.Equals(tag, "external", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "mcp", StringComparison.OrdinalIgnoreCase)) == true;

        return new ToolAccessDecision(
            IsExternal: isExternal,
            Allowed: true,
            VisibleToPlanner: true,
            RequiresAudit: isExternal,
            Reason: isExternal ? "external_tool_allowed_by_default" : "internal_tool");
    }
}

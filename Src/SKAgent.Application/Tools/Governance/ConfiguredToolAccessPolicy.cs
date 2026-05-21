using SKAgent.Core.Tools.Abstractions;

namespace SKAgent.Application.Tools.Governance;

public sealed class ToolPolicyOptions
{
    public bool AllowAllExternalTools { get; init; }
    public string[] AllowedExternalTools { get; init; } = [];
    public string[] PlannerVisibleExternalTools { get; init; } = [];
    public string[] ExternalTags { get; init; } = ["external", "mcp"];
}

public sealed class ConfiguredToolAccessPolicy : IToolAccessPolicy
{
    private readonly ToolPolicyOptions _options;
    private readonly HashSet<string> _allowedExternalTools;
    private readonly HashSet<string> _plannerVisibleExternalTools;
    private readonly HashSet<string> _externalTags;

    public ConfiguredToolAccessPolicy(ToolPolicyOptions? options = null)
    {
        _options = options ?? new ToolPolicyOptions();
        _allowedExternalTools = new HashSet<string>(
            _options.AllowedExternalTools ?? [],
            StringComparer.OrdinalIgnoreCase);
        _plannerVisibleExternalTools = new HashSet<string>(
            _options.PlannerVisibleExternalTools ?? [],
            StringComparer.OrdinalIgnoreCase);
        _externalTags = new HashSet<string>(
            _options.ExternalTags ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    public ToolAccessDecision Evaluate(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var isExternal = descriptor.Tags?.Any(tag => _externalTags.Contains(tag)) == true;
        if (!isExternal)
        {
            return new ToolAccessDecision(
                IsExternal: false,
                Allowed: true,
                VisibleToPlanner: true,
                RequiresAudit: false,
                Reason: "internal_tool");
        }

        if (_options.AllowAllExternalTools)
        {
            return new ToolAccessDecision(
                IsExternal: true,
                Allowed: true,
                VisibleToPlanner: true,
                RequiresAudit: true,
                Reason: "allow_all_external_tools");
        }

        var isAllowlisted = _allowedExternalTools.Contains(descriptor.Name);
        var isPlannerVisible = _plannerVisibleExternalTools.Count == 0
            ? isAllowlisted
            : _plannerVisibleExternalTools.Contains(descriptor.Name);
        return new ToolAccessDecision(
            IsExternal: true,
            Allowed: isAllowlisted,
            VisibleToPlanner: isPlannerVisible,
            RequiresAudit: true,
            Reason: isAllowlisted
                ? "external_tool_allowlisted"
                : isPlannerVisible
                    ? "external_tool_visible_but_blocked"
                    : "external_tool_not_allowlisted");
    }
}

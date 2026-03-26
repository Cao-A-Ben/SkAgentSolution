namespace SKAgent.Core.Tools;

public sealed record ExternalCallPolicy(
    IReadOnlySet<string> AllowTools,
    IReadOnlySet<string>? AllowScopes = null,
    IReadOnlySet<string>? DenyTools = null
);

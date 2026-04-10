namespace SKAgent.Core.Modeling;

public sealed class ModelRouteOptions
{
    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? Reason { get; set; }
}

public sealed class ModelRoutingOptions
{
    public const string SectionName = "ModelRouting";

    public ModelRouteOptions? Planner { get; set; }

    public ModelRouteOptions? Chat { get; set; }

    public ModelRouteOptions? Daily { get; set; }

    public ModelRouteOptions? Embedding { get; set; }

    public ModelRouteOptions? Rerank { get; set; }

    public ModelRouteOptions? VoiceStt { get; set; }

    public ModelRouteOptions? VoiceTts { get; set; }
}

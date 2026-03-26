namespace SKAgent.Core.Modeling;

public enum ModelPurpose
{
    Planner = 1,
    Chat = 2,
    Embedding = 3,
    Rerank = 4,
    VoiceStt = 5,
    VoiceTts = 6
}

public sealed record ModelSelection(
    ModelPurpose Purpose,
    string Provider,
    string Model,
    string Reason
);

public interface IModelRouter
{
    ModelSelection Select(ModelPurpose purpose);
}

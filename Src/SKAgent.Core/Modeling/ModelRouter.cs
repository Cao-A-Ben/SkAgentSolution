namespace SKAgent.Core.Modeling;

public enum ModelPurpose
{
    Planner = 1,
    Chat = 2,
    Daily = 3,
    Embedding = 4,
    Rerank = 5,
    VoiceStt = 6,
    VoiceTts = 7
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

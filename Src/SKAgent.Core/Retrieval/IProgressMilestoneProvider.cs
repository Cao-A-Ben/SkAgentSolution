namespace SKAgent.Core.Retrieval;

public interface IProgressMilestoneProvider
{
    Task<IReadOnlyList<string>> GetMilestonesAsync(
        string conversationId,
        string userInput,
        CancellationToken ct = default);
}

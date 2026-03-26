namespace SKAgent.Core.Memory.Facts;

public interface IFactStore
{
    Task<IReadOnlyList<FactRecord>> ListAsync(string conversationId, CancellationToken ct = default);

    Task<FactConflictDecision> UpsertAsync(
        string conversationId,
        FactRecord fact,
        CancellationToken ct = default);

    Task ClearAsync(string conversationId, CancellationToken ct = default);
}

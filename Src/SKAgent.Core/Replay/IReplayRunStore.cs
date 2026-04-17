namespace SKAgent.Core.Replay;

public interface IReplayRunStore
{
    Task SaveAsync(ReplayRunRecord record, CancellationToken ct = default);

    Task<ReplayRunRecord?> GetAsync(string runId, CancellationToken ct = default);

    Task<IReadOnlyList<ReplayRunRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default);
}

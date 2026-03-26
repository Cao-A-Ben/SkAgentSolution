namespace SKAgent.Core.Memory.Vector;

public interface IVectorStore
{
    Task<VectorUpsertResult> UpsertAsync(
        VectorRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> QueryAsync(
        VectorQuery query,
        CancellationToken cancellationToken = default);
}

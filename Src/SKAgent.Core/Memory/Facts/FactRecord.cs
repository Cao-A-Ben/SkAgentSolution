namespace SKAgent.Core.Memory.Facts;

public sealed record FactRecord(
    string Key,
    string Value,
    double Confidence,
    string Source,
    DateTimeOffset Ts,
    IReadOnlyList<string>? Tags = null
);

public enum FactConflictAction
{
    Upserted = 1,
    Skipped = 2,
    Conflict = 3
}

public sealed record FactConflictDecision(
    string Key,
    FactConflictAction Action,
    string? ExistingValue,
    string IncomingValue,
    string Reason
);

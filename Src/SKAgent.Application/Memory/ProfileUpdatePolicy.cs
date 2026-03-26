using SKAgent.Core.Memory.Facts;

namespace SKAgent.Application.Memory;

/// <summary>
/// Facts/Profile 更新冲突策略。
/// 优先级：facts > recent user statement > vector old。
/// </summary>
public sealed class ProfileUpdatePolicy
{
    public FactConflictDecision Resolve(FactRecord? existing, FactRecord incoming)
    {
        if (existing is null)
        {
            return new FactConflictDecision(
                incoming.Key,
                FactConflictAction.Upserted,
                ExistingValue: null,
                IncomingValue: incoming.Value,
                Reason: "new_fact");
        }

        if (incoming.Confidence > existing.Confidence || incoming.Ts >= existing.Ts)
        {
            return new FactConflictDecision(
                incoming.Key,
                FactConflictAction.Upserted,
                ExistingValue: existing.Value,
                IncomingValue: incoming.Value,
                Reason: "higher_confidence_or_newer");
        }

        return new FactConflictDecision(
            incoming.Key,
            FactConflictAction.Skipped,
            ExistingValue: existing.Value,
            IncomingValue: incoming.Value,
            Reason: "lower_confidence");
    }
}

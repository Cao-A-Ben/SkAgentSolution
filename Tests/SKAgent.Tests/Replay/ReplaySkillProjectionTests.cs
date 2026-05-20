using System.Text.Json;
using SKAgent.Application.Replay;
using SKAgent.Core.Replay;
using SKAgent.Core.Suggestions;
using Xunit;

namespace SKAgent.Tests.Replay;

[Collection("ReplayQueryService")]
public sealed class ReplaySkillProjectionTests : IDisposable
{
    private readonly string _agentRunDir = Path.Combine(AppContext.BaseDirectory, "data", "replay", "runs");

    [Fact]
    public async Task GetRunAsync_ShouldProjectSelectedSkillIntoRunDetail()
    {
        Directory.CreateDirectory(_agentRunDir);
        var runId = "agent-run-skill-001";
        var path = Path.Combine(_agentRunDir, $"{runId}.jsonl");

        await File.WriteAllLinesAsync(path,
        [
            Envelope(runId, 1, "2026-05-20T10:00:00Z", "run_started", new { input = "Use the MCP demo skill." }),
            Envelope(runId, 2, "2026-05-20T10:00:01Z", "persona_selected", new { conversationId = "conv-skill", personaName = "coach" }),
            Envelope(runId, 3, "2026-05-20T10:00:02Z", "skill_selected", new
            {
                conversationId = "conv-skill",
                skillName = "tech.mcp_demo",
                displayName = "Tech MCP Demo",
                description = "Demonstrates a technical skill that prefers the MCP demo tool path and concise engineering summaries.",
                recommendedTools = new[] { "mcp.demo_echo" },
                source = "request"
            }),
            Envelope(runId, 4, "2026-05-20T10:00:03Z", "run_completed", new { finalOutput = "Completed MCP demo skill run." })
        ]);

        var replayRunStore = new TestReplayRunStore();
        await replayRunStore.SaveAsync(new ReplayRunRecord(
            runId,
            "agent",
            "conv-skill",
            "completed",
            "coach",
            "MCP demo run",
            "Use the MCP demo skill.",
            "Completed MCP demo skill run.",
            DateTimeOffset.Parse("2026-05-20T10:00:00Z"),
            DateTimeOffset.Parse("2026-05-20T10:00:03Z"),
            path));

        var service = new ReplayQueryService(replayRunStore, new NoopSuggestionStore());

        var detail = await service.GetRunAsync(runId);

        Assert.NotNull(detail);
        Assert.NotNull(detail!.Skill);
        Assert.Equal("tech.mcp_demo", detail.Skill!.Name);
        Assert.Equal("Tech MCP Demo", detail.Skill.DisplayName);
        Assert.Equal("request", detail.Skill.Source);
        Assert.Contains("mcp.demo_echo", detail.Skill.RecommendedTools);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "data")))
            return;

        try
        {
            Directory.Delete(Path.Combine(AppContext.BaseDirectory, "data"), recursive: true);
        }
        catch
        {
            // Best effort cleanup for test artifacts.
        }
    }

    private static string Envelope(string runId, long seq, string timestamp, string type, object payload)
        => JsonSerializer.Serialize(new
        {
            runId,
            seq,
            ts = timestamp,
            type,
            payload
        });

    private sealed class NoopSuggestionStore : ISuggestionStore
    {
        public Task<SuggestionRecord?> GetAsync(DateOnly date, string conversationId, CancellationToken ct = default)
            => Task.FromResult<SuggestionRecord?>(null);

        public Task<SuggestionRecord?> GetByRunIdAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<SuggestionRecord?>(null);

        public Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SuggestionRecord>>(Array.Empty<SuggestionRecord>());
    }

    private sealed class TestReplayRunStore : IReplayRunStore
    {
        private readonly Dictionary<string, ReplayRunRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveAsync(ReplayRunRecord record, CancellationToken ct = default)
        {
            _records[record.RunId] = record;
            return Task.CompletedTask;
        }

        public Task<ReplayRunRecord?> GetAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<ReplayRunRecord?>(_records.TryGetValue(runId, out var record) ? record : null);

        public Task<IReadOnlyList<ReplayRunRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReplayRunRecord>>(
                _records.Values.OrderByDescending(x => x.StartedAtUtc).Take(Math.Max(1, take)).ToList());
    }
}

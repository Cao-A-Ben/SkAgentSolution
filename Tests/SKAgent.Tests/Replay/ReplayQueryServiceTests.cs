using System.Text.Json;
using SKAgent.Application.Replay;
using SKAgent.Core.Replay;
using SKAgent.Core.Suggestions;
using Xunit;

namespace SKAgent.Tests.Replay;

public sealed class ReplayQueryServiceTests : IDisposable
{
    private readonly string _agentRunDir = Path.Combine(AppContext.BaseDirectory, "data", "replay", "runs");
    private readonly string _dailyDir = Path.Combine(AppContext.BaseDirectory, "data", "daily-suggestions", "test");

    [Fact]
    public async Task ListRunsAsync_ShouldIncludeAgentAndDailyRuns()
    {
        Directory.CreateDirectory(_agentRunDir);
        Directory.CreateDirectory(_dailyDir);

        var agentRunId = "agent-run-001";
        var dailyRunId = "daily-run-001";
        var dailyPath = Path.Combine(_dailyDir, $"{dailyRunId}.jsonl");

        await WriteEventsAsync(Path.Combine(_agentRunDir, $"{agentRunId}.jsonl"),
        [
            Envelope(agentRunId, 1, "2026-04-16T10:00:00Z", "run_started", new { input = "继续推进 replay UI" }),
            Envelope(agentRunId, 2, "2026-04-16T10:00:01Z", "persona_selected", new { conversationId = "conv-agent", personaName = "coach" }),
            Envelope(agentRunId, 3, "2026-04-16T10:00:05Z", "run_completed", new { finalOutput = "先把 replay API 和 UI 列表页打通。" })
        ]);

        await WriteEventsAsync(dailyPath,
        [
            Envelope(dailyRunId, 1, "2026-04-16T11:00:00Z", "daily_job_started", new { date = "2026-04-16", conversationId = "conv-daily", personaName = "default" }),
            Envelope(dailyRunId, 2, "2026-04-16T11:00:04Z", "suggestion_saved", new { runId = dailyRunId }),
            Envelope(dailyRunId, 3, "2026-04-16T11:00:05Z", "daily_job_finished", new { runId = dailyRunId, created = true })
        ]);

        var suggestionStore = new TestSuggestionStore();
        var replayRunStore = new TestReplayRunStore();
        await replayRunStore.SaveAsync(new ReplayRunRecord(
            agentRunId,
            "agent",
            "conv-agent",
            "completed",
            "coach",
            "实现 replay API",
            "继续推进 replay UI",
            "先把 replay API 和 UI 列表页打通。",
            DateTimeOffset.Parse("2026-04-16T10:00:00Z"),
            DateTimeOffset.Parse("2026-04-16T10:00:05Z"),
            Path.Combine(_agentRunDir, $"{agentRunId}.jsonl")));
        await replayRunStore.SaveAsync(new ReplayRunRecord(
            dailyRunId,
            "daily",
            "conv-daily",
            "completed",
            "default",
            "Generate one daily suggestion for today.",
            "Daily suggestion for 2026-04-16",
            "今天先把 replay API 和 runs 页面联调通。",
            DateTimeOffset.Parse("2026-04-16T11:00:00Z"),
            DateTimeOffset.Parse("2026-04-16T11:00:05Z"),
            dailyPath));
        await suggestionStore.SaveAsync(new SuggestionRecord(
            new DateOnly(2026, 4, 16),
            "今天先把 replay API 和 runs 页面联调通。",
            dailyRunId,
            "conv-daily",
            "default",
            "profile-hash",
            "prompt-hash",
            DateTimeOffset.Parse("2026-04-16T11:00:05Z"),
            dailyPath));

        var service = new ReplayQueryService(replayRunStore, suggestionStore);

        var runs = await service.ListRunsAsync(10);

        Assert.Contains(runs, x => x.RunId == agentRunId && x.Kind == "agent" && x.PersonaName == "coach");
        Assert.Contains(runs, x => x.RunId == dailyRunId && x.Kind == "daily" && x.ConversationId == "conv-daily");
    }

    [Fact]
    public async Task GetRunAsync_ShouldProjectPromptStepsAndMemory()
    {
        Directory.CreateDirectory(_agentRunDir);
        var runId = "agent-run-detail-001";
        var path = Path.Combine(_agentRunDir, $"{runId}.jsonl");

        await WriteEventsAsync(path,
        [
            Envelope(runId, 2, "2026-04-16T10:00:01Z", "persona_selected", new { conversationId = "conv-1", personaName = "coach" }),
            Envelope(runId, 1, "2026-04-16T10:00:00Z", "run_started", new { input = "继续推进 Week9" }),
            Envelope(runId, 3, "2026-04-16T10:00:02Z", "prompt_composed", new
            {
                target = "planner",
                hash = "prompt-1",
                charBudget = 12000,
                layersUsed = new[] { "recent-history", "long-term" },
                systemChars = 100,
                userChars = 240,
                systemText = "You are a coaching assistant.",
                userText = "TASK:\n继续推进 Week9"
            }),
            Envelope(runId, 4, "2026-04-16T10:00:03Z", "plan_created", new
            {
                goal = "实现 Replay UI",
                stepCount = 1,
                steps = new[]
                {
                    new { order = 1, kind = "Agent", target = "chat" }
                }
            }),
            Envelope(runId, 5, "2026-04-16T10:00:04Z", "memory_fused", new
            {
                byRouteCounts = new Dictionary<string, int> { ["recent_history"] = 2, ["vector"] = 1 },
                totalItems = 3,
                budgetUsed = 560,
                conflictsResolved = 0
            }),
            Envelope(runId, 6, "2026-04-16T10:00:05Z", "memory_layer_included", new { layer = "long-term", countBefore = 1, countAfter = 1, budgetChars = 3200, truncateReason = "none" }),
            Envelope(runId, 7, "2026-04-16T10:00:06Z", "vector_query_executed", new { topK = 6, latencyMs = 42, scoreRange = new { min = 0.42, max = 0.93 } }),
            Envelope(runId, 8, "2026-04-16T10:00:07Z", "recall_summary_built", new { source = "recent_history+long_term+git_history", preview = "Week9 已改为独立 Replay UI 周。" }),
            Envelope(runId, 9, "2026-04-16T10:00:08Z", "step_started", new { order = 1, kind = "Agent", target = "chat" }),
            Envelope(runId, 10, "2026-04-16T10:00:09Z", "step_completed", new { order = 1, success = true, outputPreview = "先完成 replay API 与独立前端骨架。" }),
            Envelope(runId, 11, "2026-04-16T10:00:10Z", "run_completed", new { finalOutput = "Week9 第一阶段已打通。" })
        ]);

        var replayRunStore = new TestReplayRunStore();
        await replayRunStore.SaveAsync(new ReplayRunRecord(
            runId,
            "agent",
            "conv-1",
            "completed",
            "coach",
            "实现 Replay UI",
            "继续推进 Week9",
            "Week9 第一阶段已打通。",
            DateTimeOffset.Parse("2026-04-16T10:00:00Z"),
            DateTimeOffset.Parse("2026-04-16T10:00:10Z"),
            path));
        var service = new ReplayQueryService(replayRunStore, new TestSuggestionStore());

        var detail = await service.GetRunAsync(runId);
        var events = await service.GetEventsAsync(runId);

        Assert.NotNull(detail);
        Assert.Equal("coach", detail!.Summary.PersonaName);
        Assert.Equal("planner", detail.Prompt!.Target);
        Assert.Equal("You are a coaching assistant.", detail.Prompt.SystemText);
        Assert.Single(detail.Steps);
        Assert.Equal("completed", detail.Steps[0].Status);
        Assert.NotNull(detail.Memory);
        Assert.Equal("recent_history+long_term+git_history", detail.Memory!.RecallSource);
        Assert.Equal(6, detail.Memory.VectorTopK);
        Assert.True(new long[] { 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L, 11L }.SequenceEqual(events.Select(x => x.Seq)));
    }

    public void Dispose()
    {
        TryDeleteDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    }

    private static async Task WriteEventsAsync(string path, IReadOnlyList<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path, lines);
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

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for test artifacts.
        }
    }

    private sealed class TestSuggestionStore : ISuggestionStore
    {
        private readonly List<SuggestionRecord> _records = [];

        public Task<SuggestionRecord?> GetAsync(DateOnly date, string conversationId, CancellationToken ct = default)
            => Task.FromResult<SuggestionRecord?>(_records.FirstOrDefault(x => x.Date == date && x.ConversationId == conversationId));

        public Task<SuggestionRecord?> GetByRunIdAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<SuggestionRecord?>(_records.FirstOrDefault(x => x.RunId == runId));

        public Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
        {
            _records.RemoveAll(x => x.Date == record.Date && x.ConversationId == record.ConversationId);
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SuggestionRecord>>(
                _records
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Take(Math.Max(1, take))
                    .ToList());
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

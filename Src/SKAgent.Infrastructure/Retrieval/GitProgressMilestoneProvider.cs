using System.Diagnostics;
using SKAgent.Core.Retrieval;

namespace SKAgent.Infrastructure.Retrieval;

public sealed class GitProgressMilestoneProvider : IProgressMilestoneProvider
{
    public async Task<IReadOnlyList<string>> GetMilestonesAsync(
        string conversationId,
        string userInput,
        CancellationToken ct = default)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(repoRoot))
            return Array.Empty<string>();

        var subjects = await ReadCommitSubjectsAsync(repoRoot, ct);
        if (subjects.Count == 0)
            return Array.Empty<string>();

        var milestones = new List<string>();

        void AddIfMatched(string summary, params string[] keywords)
        {
            if (subjects.Any(subject => keywords.Any(k => subject.Contains(k, StringComparison.OrdinalIgnoreCase)))
                && !milestones.Contains(summary, StringComparer.OrdinalIgnoreCase))
            {
                milestones.Add(summary);
            }
        }

        AddIfMatched(
            "persona 切换与 coach 风格能力",
            "persona", "coach");
        AddIfMatched(
            "Daily Suggestion 的生成、幂等与内容优化",
            "daily suggestion", "suggestion", "conversation scope", "daily_suggestions");
        AddIfMatched(
            "planner / chat / daily / embedding 的模型路由收敛",
            "model routing", "embedding routing", "configurable model routing", "embedding");
        AddIfMatched(
            "rerank 接入与检索链路增强",
            "rerank", "retrieval", "progress recall");
        AddIfMatched(
            "真实环境验收、回归和事件链验证",
            "acceptance", "docs", "replay", "phase1");

        return milestones.Take(3).ToArray();
    }

    private static async Task<IReadOnlyList<string>> ReadCommitSubjectsAsync(string repoRoot, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "log --pretty=%s -n 30",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            return Array.Empty<string>();

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static string? FindRepoRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}

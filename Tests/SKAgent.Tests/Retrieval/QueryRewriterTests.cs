using SKAgent.Application.Retrieval;
using SKAgent.Core.Retrieval;
using Xunit;

namespace SKAgent.Tests.Retrieval;

public sealed class QueryRewriterTests
{
    private readonly QueryRewriter _rewriter = new();

    [Fact]
    public async Task RewriteAsync_ShouldExpandProgressSummaryRecall_IntoFocusedQueries()
    {
        var queries = await _rewriter.RewriteAsync(
            "总结一下我最近在 Week8 到 Week8.5 主要推进了什么",
            RetrievalIntent.Recall);

        Assert.Equal(3, queries.Count);
        Assert.Contains("总结一下我最近在 Week8 到 Week8.5 主要推进了什么", queries);
        Assert.Contains(queries, q => q.Contains("功能主题与里程碑", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(queries, q => q.Contains("persona、coach、Daily Suggestion、幂等、conversation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(queries, q => q.Contains("planner、chat、daily、embedding、model routing、rerank", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RewriteAsync_ShouldKeepGenericRecallRewrite_ForNormalRecall()
    {
        var queries = await _rewriter.RewriteAsync(
            "我之前的目标是什么",
            RetrievalIntent.Recall);

        Assert.Equal(2, queries.Count);
        Assert.Contains("我之前的目标是什么", queries);
        Assert.Contains(queries, q => q.Contains("长期目标/偏好", StringComparison.OrdinalIgnoreCase));
    }
}

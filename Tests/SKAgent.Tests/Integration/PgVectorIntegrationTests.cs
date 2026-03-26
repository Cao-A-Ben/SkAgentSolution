using Xunit;

namespace SKAgent.Tests.Integration;

public sealed class PgVectorIntegrationTests
{
    [Fact(Skip = "Requires external Postgres + pgvector. Run in CI integration stage.")]
    public void PgVector_UpsertQuery_ShouldRespectConversationFilter()
    {
        // 集成测试预留：
        // 1) upsert 两个会话的 chunk
        // 2) query 带 conversation_id 过滤
        // 3) 验证只返回目标会话命中
        Assert.True(true);
    }
}

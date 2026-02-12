namespace SKAgent.Host.Contracts
{
    /// <summary>
    /// 【Host 层 - API 请求 DTO】
    /// 客户端发送到 POST /api/agent/run 的请求体。
    /// 客户端只需保存 conversationId 即可持续对话。
    /// </summary>
    public class AgentRunRequest
    {
        /// <summary>
        /// 会话 ID（可选）。
        /// - 若为空或未提供，服务端会自动生成新的会话 ID。
        /// - 若提供，则复用已有会话，加载其历史记忆和画像。
        /// </summary>
        public string? ConversationId { get; set; }

        /// <summary>
        /// 用户输入文本（必填）。
        /// </summary>
        public string Input { get; init; } = string.Empty;
    }
}

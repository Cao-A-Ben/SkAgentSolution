namespace SKAgent.Host.Contracts
{
    /// <summary>
    /// 【Host 层 - API 响应 DTO】
    /// POST /api/agent/run 的响应体，将 AgentRunContext 映射为客户端友好的结构。
    /// 包含会话 ID、运行 ID、目标、状态、输出、画像快照和步骤明细。
    /// </summary>
    public class AgentRunResponse
    {
        /// <summary>会话唯一标识 ID，客户端应保存此值用于后续请求。</summary>
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>本次运行的唯一标识 ID，用于日志追踪和审计。</summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>PlannerAgent 生成的目标描述。</summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>运行状态（Completed / Failed 等）。</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>最终聚合输出文本，由所有步骤的 Output 拼接而成。</summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// 用户画像快照（Week4 新增）。
        /// 包含本次回合后的完整画像数据，用于客户端验收和展示。
        /// </summary>
        public Dictionary<string, string>? ProfileSnapshot { get; init; }

        /// <summary>执行步骤明细列表，每个元素对应一个 PlanStep 的执行结果。</summary>
        public IReadOnlyList<AgentStepResponse> Steps { get; init; } = Array.Empty<AgentStepResponse>();
    }

    /// <summary>
    /// 【Host 层 - 步骤响应 DTO】
    /// 单个执行步骤的响应数据，嵌套在 AgentRunResponse.Steps 中。
    /// 由 PlanStepExecution 映射而来。
    /// </summary>
    public sealed class AgentStepResponse
    {
        /// <summary>步骤执行顺序。</summary>
        public int Order { get; init; }

        /// <summary>执行该步骤的 Agent 名称。</summary>
        public string Agent { get; init; } = string.Empty;

        /// <summary>步骤执行状态（Success / Failed 等）。</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>步骤输出文本。</summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>步骤执行失败时的错误信息。</summary>
        public string? Error { get; init; }
    }
}

using SkAgent.Runtime.Runtime;
using SKAgent.Core.Voice;

namespace SKAgent.Host.Voice;

/// <summary>
/// Runtime 到 Voice 用例的适配器。
/// 它把现有 AgentRuntimeService 包装为 `IVoiceAgentRuntime`，供 Application 的语音编排服务调用。
/// </summary>
public sealed class AgentVoiceRuntimeAdapter : IVoiceAgentRuntime
{
    private readonly AgentRuntimeService _agentRuntimeService;

    public AgentVoiceRuntimeAdapter(AgentRuntimeService agentRuntimeService)
    {
        _agentRuntimeService = agentRuntimeService;
    }

    /// <summary>
    /// 以语音链路上下文调用现有文本 Runtime，并把结果裁剪成稳定的最小返回模型。
    /// </summary>
    public async Task<VoiceAgentRuntimeResult> RunAsync(
        VoiceAgentRuntimeRequest request,
        CancellationToken ct = default)
    {
        var run = await _agentRuntimeService.RunAsync(
            request.ConversationId,
            request.Input,
            request.PersonaName,
            request.RunId,
            request.EventSink,
            initialEventSeq: request.InitialEventSeq,
            ct: ct);

        return new VoiceAgentRuntimeResult(
            ConversationId: run.ConversationId,
            RunId: run.RunId,
            Status: run.Status.ToString().ToLowerInvariant(),
            PersonaName: run.ConversationState.TryGetValue("personaName", out var personaName) ? personaName as string : null,
            Goal: string.IsNullOrWhiteSpace(run.Goal) ? null : run.Goal,
            FinalOutput: run.FinalOutput,
            EventSeq: run.EventSeq);
    }
}

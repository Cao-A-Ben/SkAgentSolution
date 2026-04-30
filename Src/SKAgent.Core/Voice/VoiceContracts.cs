using System.IO;
using SKAgent.Core.Observability;

namespace SKAgent.Core.Voice;

/// <summary>
/// STT 请求载荷。
/// 这里不绑定具体 provider，而是只表达“有一段音频流需要被转成文本”。
/// </summary>
public sealed record VoiceTranscriptionRequest(
    Stream Audio,
    string FileName,
    string? ContentType,
    string Model
);

/// <summary>
/// STT 结果。
/// 除了转写文本外，同时保留 provider / model，便于事件审计与回放。
/// </summary>
public sealed record VoiceTranscriptionResult(
    string Text,
    string Provider,
    string Model
);

/// <summary>
/// TTS 请求载荷。
/// Week10 先只关心最小闭环，因此只保留 input / model / voice / format 四个关键字段。
/// </summary>
public sealed record VoiceSynthesisRequest(
    string Input,
    string Model,
    string Voice,
    string Format
);

/// <summary>
/// TTS 结果。
/// 返回可直接下发给客户端播放的音频字节与内容类型。
/// </summary>
public sealed record VoiceSynthesisResult(
    byte[] AudioBytes,
    string ContentType,
    string Format,
    string Provider,
    string Model
);

/// <summary>
/// 语音转写服务抽象。
/// Week10 先接一个默认实现，后续可替换为本地、腾讯、Google 等 provider。
/// </summary>
public interface IVoiceTranscriptionService
{
    /// <summary>
    /// 将音频流转为文本。
    /// </summary>
    Task<VoiceTranscriptionResult> TranscribeAsync(
        VoiceTranscriptionRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// 语音合成服务抽象。
/// </summary>
public interface IVoiceSynthesisService
{
    /// <summary>
    /// 将文本转为音频字节。
    /// </summary>
    Task<VoiceSynthesisResult> SynthesizeAsync(
        VoiceSynthesisRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Voice Runtime 的最小运行时选项。
/// </summary>
public sealed class VoiceRuntimeOptions
{
    public const string SectionName = "VoiceRuntime";

    /// <summary>
    /// 默认 TTS 音色。
    /// Kokoro 本地服务优先使用真实 voice 名，而不是 OpenAI alias。
    /// </summary>
    public string DefaultVoice { get; set; } = "af_bella";

    /// <summary>
    /// 默认输出音频格式。
    /// </summary>
    public string DefaultFormat { get; set; } = "mp3";

    /// <summary>
    /// 为避免把极长文本直接送入 TTS，这里对合成文本长度做一个 MVP 阶段上限。
    /// 语音播报不适合完整复述长 Markdown，因此这里默认值应明显低于普通聊天输出长度。
    /// </summary>
    public int MaxSynthesisChars { get; set; } = 600;
}

/// <summary>
/// 语音编排层复用现有 Runtime 时所需的最小输入模型。
/// Application 不直接依赖 AgentRunContext，而是通过这个稳定契约与 Runtime 协作。
/// </summary>
public sealed record VoiceAgentRuntimeRequest(
    string ConversationId,
    string Input,
    string? PersonaName,
    string RunId,
    IRunEventSink EventSink,
    long InitialEventSeq
);

/// <summary>
/// Runtime 执行完成后返回给语音编排层的最小结果。
/// </summary>
public sealed record VoiceAgentRuntimeResult(
    string ConversationId,
    string RunId,
    string Status,
    string? PersonaName,
    string? Goal,
    string? FinalOutput,
    long EventSeq
);

/// <summary>
/// 现有文本 Runtime 的语音适配器抽象。
/// 这样 Voice 用例可以待在 Application，而不需要直接依赖 Runtime 工程。
/// </summary>
public interface IVoiceAgentRuntime
{
    /// <summary>
    /// 以语音链路的上下文调用现有 Agent Runtime。
    /// </summary>
    Task<VoiceAgentRuntimeResult> RunAsync(
        VoiceAgentRuntimeRequest request,
        CancellationToken ct = default);
}

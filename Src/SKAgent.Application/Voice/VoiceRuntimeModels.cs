namespace SKAgent.Application.Voice;

/// <summary>
/// Voice Runtime 的应用层输入模型。
/// Host 可以用 multipart/form-data 接收音频，但进入应用层后统一收敛为这个模型。
/// </summary>
public sealed record VoiceRunInput(
    string ConversationId,
    string? PersonaName,
    string FileName,
    string? ContentType,
    long? Length,
    Stream Audio,
    string? Voice,
    string? Format
);

/// <summary>
/// Voice Runtime 的应用层输出模型。
/// Week10 先返回文本、音频字节和 replay 所需标识，后续可再拆成下载流或分段播放模型。
/// </summary>
public sealed record VoiceRunResult(
    string ConversationId,
    string RunId,
    string Transcript,
    string OutputText,
    byte[] AudioBytes,
    string AudioContentType,
    string AudioFormat
);

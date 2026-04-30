using Microsoft.AspNetCore.Http;

namespace SKAgent.Host.Contracts.Voice;

/// <summary>
/// Voice run 的表单请求。
/// Week10 先采用 multipart/form-data，避免把音频强行塞进 base64 JSON。
/// </summary>
public sealed class VoiceRunRequest
{
    /// <summary>
    /// 会话 ID。
    /// 若为空则由服务端自动生成，语音 run 也会和普通对话一样进入同一会话体系。
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// 本次 run 的可选 persona。
    /// </summary>
    public string? PersonaName { get; set; }

    /// <summary>
    /// 可选 TTS 音色。
    /// 未提供时回落到 VoiceRuntime 默认值。
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// 可选输出音频格式。
    /// 未提供时回落到 VoiceRuntime 默认值。
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 上传的音频文件。
    /// </summary>
    public IFormFile Audio { get; set; } = default!;
}

namespace SKAgent.Host.Contracts.Voice;

/// <summary>
/// Voice run 的响应 DTO。
/// Week10 MVP 先同时返回 transcript / text / audioBase64，便于前端和手工验收快速打通。
/// </summary>
public sealed class VoiceRunResponse
{
    public string ConversationId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string Transcript { get; set; } = string.Empty;

    public string OutputText { get; set; } = string.Empty;

    public string AudioBase64 { get; set; } = string.Empty;

    public string AudioContentType { get; set; } = "audio/mpeg";

    public string AudioFormat { get; set; } = "mp3";
}

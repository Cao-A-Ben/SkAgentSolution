namespace SKAgent.Infrastructure.Voice;

/// <summary>
/// 语音网关连接配置。
/// Week10 允许语音链路独立于通用聊天网关配置，便于后续单独切换到别的 openai-compatible provider。
/// </summary>
public sealed class VoiceGatewayOptions
{
    public const string SectionName = "VoiceGateway";

    /// <summary>
    /// 语音 provider 的基地址。
    /// 留空时回退到通用 OpenAI 配置。
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 语音 provider 的 API Key。
    /// 留空时回退到通用 OpenAI 配置。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 语音 HTTP 请求超时时间，单位秒。
    /// 本地 Whisper 首次加载模型可能明显慢于默认 100 秒，因此这里提供独立超时配置。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

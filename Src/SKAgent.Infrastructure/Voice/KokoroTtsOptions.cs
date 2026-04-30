namespace SKAgent.Infrastructure.Voice;

/// <summary>
/// 本地 Kokoro TTS 配置。
/// 默认按 OpenAI-compatible `POST /v1/audio/speech` 端点对接本地 Kokoro 服务。
/// </summary>
public sealed class KokoroTtsOptions
{
    public const string SectionName = "KokoroTts";

    /// <summary>
    /// 本地 Kokoro 服务的基地址。
    /// 例如 `http://127.0.0.1:8880`。
    /// </summary>
    public string? BaseUrl { get; set; } = "http://127.0.0.1:8880";

    /// <summary>
    /// 可选的本地服务 API Key。
    /// 若本地部署未开启鉴权，则保持为空。
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Kokoro TTS 的客户端超时秒数。
    /// 远端 CPU 合成可能明显慢于本地开发机，因此单独提供更宽松的超时窗口。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

using System.Text;
using System.Text.Json;
using System.Net;
using SKAgent.Core.Voice;

namespace SKAgent.Infrastructure.Voice;

/// <summary>
/// 本地 Kokoro TTS 实现。
/// 约定下游服务暴露 OpenAI-compatible `POST /v1/audio/speech` 接口。
/// </summary>
public sealed class KokoroVoiceSynthesisService : IVoiceSynthesisService
{
    private readonly HttpClient _httpClient;

    public KokoroVoiceSynthesisService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 调用本地 Kokoro 服务把文本合成为音频。
    /// </summary>
    public async Task<VoiceSynthesisResult> SynthesizeAsync(
        VoiceSynthesisRequest request,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = request.Model,
            input = request.Input,
            voice = request.Voice,
            response_format = request.Format
        });

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/audio/speech")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        message.Version = HttpVersion.Version11;
        message.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        try
        {
            using var response = await _httpClient.SendAsync(message, ct);
            var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
            EnsureSuccessStatusCode(response, GetBodyPreview(audioBytes));

            return new VoiceSynthesisResult(
                AudioBytes: audioBytes,
                ContentType: response.Content.Headers.ContentType?.ToString() ?? GetContentType(request.Format),
                Format: request.Format,
                Provider: "kokoro-local",
                Model: request.Model);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"Kokoro TTS request timed out for voice '{request.Voice}' with {request.Input.Length} chars.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Kokoro TTS request failed for voice '{request.Voice}' with {request.Input.Length} chars: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// 本地服务失败时，尽量把响应体抛出来，便于快速定位配置或模型问题。
    /// </summary>
    private static void EnsureSuccessStatusCode(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var preview = string.IsNullOrWhiteSpace(body) ? "<empty>" : body.Trim();

        if (preview.Length > 1000)
        {
            preview = preview[..1000] + "...";
        }

        throw new HttpRequestException(
            $"Kokoro TTS request failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {preview}");
    }

    /// <summary>
    /// 二进制音频错误体可能仍是 JSON 或文本，这里做保守预览。
    /// </summary>
    private static string GetBodyPreview(byte[] body)
    {
        if (body.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return $"<{body.Length} bytes>";
        }
    }

    private static string GetContentType(string format)
        => format.ToLowerInvariant() switch
        {
            "wav" => "audio/wav",
            "opus" => "audio/opus",
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            "pcm" => "audio/pcm",
            _ => "audio/mpeg"
        };
}

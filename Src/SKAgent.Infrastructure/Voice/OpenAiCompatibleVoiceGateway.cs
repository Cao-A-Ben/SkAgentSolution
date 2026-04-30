using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SKAgent.Core.Voice;

namespace SKAgent.Infrastructure.Voice;

/// <summary>
/// OpenAI-compatible 语音网关。
/// Week10 先以一条默认 provider 路径打通 STT / TTS，但对上层仍暴露统一抽象接口。
/// </summary>
public sealed class OpenAiCompatibleVoiceGateway :
    IVoiceTranscriptionService,
    IVoiceSynthesisService
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleVoiceGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 调用兼容 `/audio/transcriptions` 的接口执行 STT。
    /// </summary>
    public async Task<VoiceTranscriptionResult> TranscribeAsync(
        VoiceTranscriptionRequest request,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var audioContent = new StreamContent(request.Audio);

        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        }

        form.Add(audioContent, "file", request.FileName);
        form.Add(new StringContent(request.Model), "model");

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync("audio/transcriptions", form, ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"OpenAI-compatible transcription request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds.",
                ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            EnsureSuccessStatusCode(response, body, "transcription");

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.TryGetProperty("text", out var node)
                ? node.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Voice transcription response did not include text.");
            }

            return new VoiceTranscriptionResult(
                Text: text.Trim(),
                Provider: "openai-compatible",
                Model: request.Model);
        }
    }

    /// <summary>
    /// 调用兼容 `/audio/speech` 的接口执行 TTS。
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

        using var message = new HttpRequestMessage(HttpMethod.Post, "audio/speech")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException(
                $"OpenAI-compatible speech synthesis request timed out after {_httpClient.Timeout.TotalSeconds:0} seconds.",
                ex);
        }

        using (response)
        {
            var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
            EnsureSuccessStatusCode(response, GetBodyPreview(audioBytes), "speech synthesis");

            return new VoiceSynthesisResult(
                AudioBytes: audioBytes,
                ContentType: response.Content.Headers.ContentType?.ToString() ?? GetContentType(request.Format),
                Format: request.Format,
                Provider: "openai-compatible",
                Model: request.Model);
        }
    }

    private static string GetContentType(string format)
        => format.ToLowerInvariant() switch
        {
            "wav" => "audio/wav",
            "opus" => "audio/opus",
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            _ => "audio/mpeg"
        };

    /// <summary>
    /// 把 provider 返回体带进异常，方便定位模型、参数或鉴权错误。
    /// </summary>
    private static void EnsureSuccessStatusCode(
        HttpResponseMessage response,
        string body,
        string operation)
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
            $"OpenAI-compatible {operation} request failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {preview}");
    }

    /// <summary>
    /// 二进制音频错误体通常仍是文本，这里做一个保守预览，避免异常消息过大。
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
}

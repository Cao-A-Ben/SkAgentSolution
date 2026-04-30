using Microsoft.AspNetCore.Mvc;
using SKAgent.Application.Voice;
using SKAgent.Host.Contracts.Voice;

namespace SKAgent.Host.Controllers;

/// <summary>
/// Week10 语音入口控制器。
/// 当前只提供一个最小闭环端点：上传音频 -> STT -> Runtime -> TTS。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class VoiceController : ControllerBase
{
    private readonly VoiceRuntimeService _voiceRuntimeService;

    public VoiceController(VoiceRuntimeService voiceRuntimeService)
    {
        _voiceRuntimeService = voiceRuntimeService;
    }

    /// <summary>
    /// 执行一次语音 run。
    /// 请求形态为 multipart/form-data，至少包含 `audio` 文件字段。
    /// </summary>
    [HttpPost("run")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<VoiceRunResponse>> Run([FromForm] VoiceRunRequest request)
    {
        if (request.Audio is null || request.Audio.Length == 0)
        {
            return BadRequest("Audio file is required.");
        }

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId.Trim();

        try
        {
            await using var audioStream = request.Audio.OpenReadStream();
            var result = await _voiceRuntimeService.RunAsync(
                new VoiceRunInput(
                    ConversationId: conversationId,
                    PersonaName: request.PersonaName,
                    FileName: string.IsNullOrWhiteSpace(request.Audio.FileName) ? "voice-input" : request.Audio.FileName,
                    ContentType: request.Audio.ContentType,
                    Length: request.Audio.Length,
                    Audio: audioStream,
                    Voice: request.Voice,
                    Format: request.Format),
                HttpContext.RequestAborted);

            return Ok(new VoiceRunResponse
            {
                ConversationId = result.ConversationId,
                RunId = result.RunId,
                Transcript = result.Transcript,
                OutputText = result.OutputText,
                AudioBase64 = Convert.ToBase64String(result.AudioBytes),
                AudioContentType = result.AudioContentType,
                AudioFormat = result.AudioFormat
            });
        }
        catch (HttpRequestException ex)
        {
            return Problem(
                title: "Voice provider request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                title: "Voice runtime produced an invalid response",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

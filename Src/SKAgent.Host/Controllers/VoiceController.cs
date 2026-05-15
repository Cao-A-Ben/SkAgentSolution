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
    private readonly VoiceOrchestrationService _voiceOrchestrationService;

    /// <summary>
    /// Controller 只负责 HTTP 边界：
    /// 1. 校验 multipart/form-data 与音频文件是否存在
    /// 2. 把表单模型映射为 Application 层的 VoiceRunInput
    /// 3. 把语音 provider / runtime 异常收敛成稳定的 HTTP 响应
    ///
    /// 真正的 STT -> Runtime -> TTS 编排逻辑不应写在 Controller，
    /// 否则就无法复用、测试，也会把 HTTP 细节和业务流程耦在一起。
    /// </summary>
    public VoiceController(VoiceOrchestrationService voiceOrchestrationService)
    {
        _voiceOrchestrationService = voiceOrchestrationService;
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

        // 语音 run 也进入同一套 conversation/replay 体系；
        // 如果客户端没传 conversationId，就由服务端兜底生成一个。
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId.Trim();

        try
        {
            // 这里不把 IFormFile 直接向下传递，而是只传只读流和必要元数据。
            // 这样 Application 层不需要依赖 ASP.NET Core 的 HTTP 抽象。
            await using var audioStream = request.Audio.OpenReadStream();
            var result = await _voiceOrchestrationService.RunAsync(
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

            // MVP 阶段先直接把音频做成 base64 返回，便于接口联调和 Swagger 验证。
            // 后续如果进入更正式的播放体验，可以再切成文件流、下载地址或流式音频响应。
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
            // provider 层失败统一归并成 502，避免把底层网络异常直接暴露成框架错误页。
            return Problem(
                title: "Voice provider request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException ex)
        {
            // 这类错误通常说明 runtime 或 provider 返回了结构上不完整的数据。
            return Problem(
                title: "Voice runtime produced an invalid response",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

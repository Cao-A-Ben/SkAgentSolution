using System.Text.Json;
using System.Text.RegularExpressions;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Replay;
using SKAgent.Core.Voice;

namespace SKAgent.Application.Voice;

/// <summary>
/// Week10 的语音编排服务。
/// Application 负责串联 STT / Runtime / TTS，但不直接依赖具体 Runtime 实现。
/// </summary>
public sealed class VoiceRuntimeService
{
    private static readonly Regex MarkdownDecorationRegex = new(@"(\*\*|__|`|#+\s*)", RegexOptions.Compiled);
    private static readonly Regex ListPrefixRegex = new(@"^\s*(?:[-*+]|\d+\.)\s*", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly IVoiceAgentRuntime _voiceAgentRuntime;
    private readonly IModelRouter _modelRouter;
    private readonly IVoiceTranscriptionService _transcriptionService;
    private readonly IVoiceSynthesisService _synthesisService;
    private readonly IRunEventLogFactory _runEventLogFactory;
    private readonly IReplayRunStore _replayRunStore;
    private readonly VoiceRuntimeOptions _options;

    public VoiceRuntimeService(
        IVoiceAgentRuntime voiceAgentRuntime,
        IModelRouter modelRouter,
        IVoiceTranscriptionService transcriptionService,
        IVoiceSynthesisService synthesisService,
        IRunEventLogFactory runEventLogFactory,
        IReplayRunStore replayRunStore,
        VoiceRuntimeOptions options)
    {
        _voiceAgentRuntime = voiceAgentRuntime;
        _modelRouter = modelRouter;
        _transcriptionService = transcriptionService;
        _synthesisService = synthesisService;
        _runEventLogFactory = runEventLogFactory;
        _replayRunStore = replayRunStore;
        _options = options;
    }

    /// <summary>
    /// 执行一次完整语音 run：
    /// 1. 音频输入落事件
    /// 2. STT 转写
    /// 3. 通过 Runtime 适配器执行现有文本链路
    /// 4. TTS 合成可播放音频
    /// 5. 保存 replay 元数据索引
    /// </summary>
    public async Task<VoiceRunResult> RunAsync(VoiceRunInput input, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid().ToString("N");
        var eventLog = _runEventLogFactory.CreateAgentRunLog(runId);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var seq = 0L;

        await EmitAsync(
            eventLog.Sink,
            runId,
            ++seq,
            "voice_input_received",
            new
            {
                fileName = input.FileName,
                contentType = input.ContentType,
                length = input.Length,
                conversationId = input.ConversationId
            },
            ct);

        var sttSelection = _modelRouter.Select(ModelPurpose.VoiceStt);
        await EmitModelSelectedAsync(eventLog.Sink, runId, ++seq, sttSelection, "voice_stt", ct);

        var transcription = await _transcriptionService.TranscribeAsync(
            new VoiceTranscriptionRequest(
                Audio: input.Audio,
                FileName: input.FileName,
                ContentType: input.ContentType,
                Model: sttSelection.Model),
            ct);

        await EmitAsync(
            eventLog.Sink,
            runId,
            ++seq,
            "voice_transcribed",
            new
            {
                chars = transcription.Text.Length,
                preview = Trim(transcription.Text, 160),
                provider = transcription.Provider,
                model = transcription.Model
            },
            ct);

        var runtimeResult = await _voiceAgentRuntime.RunAsync(
            new VoiceAgentRuntimeRequest(
                ConversationId: input.ConversationId,
                Input: transcription.Text,
                PersonaName: input.PersonaName,
                RunId: runId,
                EventSink: eventLog.Sink,
                InitialEventSeq: seq),
            ct);

        seq = runtimeResult.EventSeq;

        var ttsSelection = _modelRouter.Select(ModelPurpose.VoiceTts);
        await EmitModelSelectedAsync(eventLog.Sink, runId, ++seq, ttsSelection, "voice_tts", ct);

        var outputText = string.IsNullOrWhiteSpace(runtimeResult.FinalOutput)
            ? transcription.Text
            : runtimeResult.FinalOutput!;
        var ttsInput = PrepareSpeechText(outputText, _options.MaxSynthesisChars);

        var synthesis = await _synthesisService.SynthesizeAsync(
            new VoiceSynthesisRequest(
                Input: ttsInput,
                Model: ttsSelection.Model,
                Voice: string.IsNullOrWhiteSpace(input.Voice) ? _options.DefaultVoice : input.Voice!,
                Format: string.IsNullOrWhiteSpace(input.Format) ? _options.DefaultFormat : input.Format!),
            ct);

        await EmitAsync(
            eventLog.Sink,
            runId,
            ++seq,
            "voice_response_synthesized",
            new
            {
                chars = ttsInput.Length,
                voice = string.IsNullOrWhiteSpace(input.Voice) ? _options.DefaultVoice : input.Voice,
                format = synthesis.Format,
                provider = synthesis.Provider,
                model = synthesis.Model
            },
            ct);

        await EmitAsync(
            eventLog.Sink,
            runId,
            ++seq,
            "voice_playback_ready",
            new
            {
                bytes = synthesis.AudioBytes.Length,
                contentType = synthesis.ContentType,
                format = synthesis.Format
            },
            ct);

        await _replayRunStore.SaveAsync(
            new ReplayRunRecord(
                RunId: runtimeResult.RunId,
                Kind: "voice",
                ConversationId: runtimeResult.ConversationId,
                Status: runtimeResult.Status,
                PersonaName: runtimeResult.PersonaName,
                Goal: string.IsNullOrWhiteSpace(runtimeResult.Goal) ? "voice_runtime" : runtimeResult.Goal,
                InputPreview: Trim(transcription.Text, 240),
                FinalOutputPreview: Trim(runtimeResult.FinalOutput, 240),
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: DateTimeOffset.UtcNow,
                EventLogPath: eventLog.Path),
            ct);

        return new VoiceRunResult(
            ConversationId: runtimeResult.ConversationId,
            RunId: runtimeResult.RunId,
            Transcript: transcription.Text,
            OutputText: outputText,
            AudioBytes: synthesis.AudioBytes,
            AudioContentType: synthesis.ContentType,
            AudioFormat: synthesis.Format);
    }

    /// <summary>
    /// 语音链路的前后置事件不经过 AgentRunContext，因此这里直接写 RunEvent。
    /// 然后把 seq 继续传给 Runtime，保证一条 replay 时间线内的顺序连续。
    /// </summary>
    private static async Task EmitAsync(
        IRunEventSink sink,
        string runId,
        long seq,
        string type,
        object payload,
        CancellationToken ct)
    {
        var element = JsonSerializer.SerializeToElement(payload);
        await sink.WriteAsync(
            new RunEvent(
                RunId: runId,
                TsUtc: DateTimeOffset.UtcNow,
                Seq: seq,
                Type: type,
                Payload: element),
            ct);
    }

    private static Task EmitModelSelectedAsync(
        IRunEventSink sink,
        string runId,
        long seq,
        ModelSelection selection,
        string purposeName,
        CancellationToken ct)
    {
        return EmitAsync(
            sink,
            runId,
            seq,
            "model_selected",
            new
            {
                purpose = purposeName,
                provider = selection.Provider,
                model = selection.Model,
                reason = selection.Reason
            },
            ct);
    }

    private static string Trim(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxChars
            ? value
            : value[..maxChars].TrimEnd() + "...";
    }

    /// <summary>
    /// 语音播报不适合直接朗读 Markdown 或超长结构化文本。
    /// 这里做最小规范化，让 TTS 输入更接近自然口播。
    /// </summary>
    private static string PrepareSpeechText(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        normalized = MarkdownDecorationRegex.Replace(normalized, string.Empty);
        normalized = ListPrefixRegex.Replace(normalized, string.Empty);
        normalized = normalized.Replace('\n', ' ');
        normalized = WhitespaceRegex.Replace(normalized, " ").Trim();

        return Trim(normalized, maxChars);
    }
}

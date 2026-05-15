using System.Text;
using System.Text.Json;
using SKAgent.Application.Voice;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Replay;
using SKAgent.Core.Voice;
using Xunit;

namespace SKAgent.Tests.Voice;

/// <summary>
/// 这里测试的不是 ASP.NET Controller，而是 Application 层的 VoiceOrchestrationService。
/// 原因是 Week10 的核心价值在“编排逻辑”：
/// STT 选模 -> 转写 -> 复用文本 Runtime -> TTS 选模 -> replay 落事件。
/// 这部分一旦稳定，Controller 只剩 HTTP 绑定和异常映射，测试价值反而较低。
/// </summary>
public sealed class VoiceOrchestrationServiceTests
{
    [Fact]
    public async Task RunAsync_ShouldEmitVoiceEvents_AndPersistReplayMetadata()
    {
        // 用内存 sink / store 替代真实日志与数据库，专注验证：
        // 1. 事件顺序是否正确
        // 2. replay 元数据是否按 voice run 的口径落盘
        var eventSink = new CapturingRunEventSink();
        var eventLogFactory = new TestRunEventLogFactory(eventSink, "voice-run.jsonl");
        var replayRunStore = new TestReplayRunStore();
        var runtime = new TestVoiceAgentRuntime((request, ct) =>
        {
            // VoiceOrchestrationService 在进入文本 Runtime 之前已经写入了 3 个前置事件：
            // voice_input_received / model_selected(voice_stt) / voice_transcribed
            Assert.Equal(3, request.InitialEventSeq);

            return CompleteRuntimeAsync(
                request,
                "Voice runtime reply",
                finalSeq: 4,
                ct);
        });

        var service = new VoiceOrchestrationService(
            runtime,
            new TestModelRouter(),
            new TestTranscriptionService("请帮我总结一下今天的进展"),
            new TestSynthesisService(),
            eventLogFactory,
            replayRunStore,
            new VoiceRuntimeOptions
            {
                DefaultVoice = "alloy",
                DefaultFormat = "mp3",
                MaxSynthesisChars = 2400
            });

        await using var audio = new MemoryStream(Encoding.UTF8.GetBytes("fake-audio"));
        var result = await service.RunAsync(
            new VoiceRunInput(
                ConversationId: "conv-voice-1",
                PersonaName: "coach",
                FileName: "sample.wav",
                ContentType: "audio/wav",
                Length: audio.Length,
                Audio: audio,
                Voice: "nova",
                Format: "wav"));

        Assert.Equal("conv-voice-1", result.ConversationId);
        Assert.Equal("请帮我总结一下今天的进展", result.Transcript);
        Assert.Equal("Voice runtime reply", result.OutputText);
        Assert.Equal("audio/wav", result.AudioContentType);
        Assert.Equal("wav", result.AudioFormat);

        var saved = Assert.Single(replayRunStore.Records);
        Assert.Equal("voice", saved.Kind);
        Assert.Equal("coach", saved.PersonaName);
        Assert.Equal("completed", saved.Status);
        Assert.Equal("voice-run.jsonl", saved.EventLogPath);
        Assert.Equal("请帮我总结一下今天的进展", saved.InputPreview);
        Assert.Equal("Voice runtime reply", saved.FinalOutputPreview);

        Assert.Collection(
            eventSink.Events.OrderBy(x => x.Seq),
            evt => Assert.Equal("voice_input_received", evt.Type),
            evt => AssertModelSelected(evt, "voice_stt", "test-stt-model"),
            evt => Assert.Equal("voice_transcribed", evt.Type),
            evt => Assert.Equal("run_completed", evt.Type),
            evt => AssertModelSelected(evt, "voice_tts", "test-tts-model"),
            evt => Assert.Equal("voice_response_synthesized", evt.Type),
            evt => Assert.Equal("voice_playback_ready", evt.Type));
    }

    [Fact]
    public async Task RunAsync_ShouldUseDefaultVoiceAndTrimTtsInput()
    {
        // 这个测试专门验证两件事：
        // 1. 客户端没传 voice / format 时是否正确回落到默认值
        // 2. Runtime 输出过长时，送给 TTS 的文本是否被截断
        var eventLogFactory = new TestRunEventLogFactory(new CapturingRunEventSink(), "voice-run-2.jsonl");
        var replayRunStore = new TestReplayRunStore();
        var synthesis = new TestSynthesisService();
        var longOutput = new string('A', 40);

        var service = new VoiceOrchestrationService(
            new TestVoiceAgentRuntime((request, ct) =>
                Task.FromResult(new VoiceAgentRuntimeResult(
                    request.ConversationId,
                    request.RunId,
                    "completed",
                    "default",
                    "voice_runtime",
                    longOutput,
                    request.InitialEventSeq + 1))),
            new TestModelRouter(),
            new TestTranscriptionService("hello"),
            synthesis,
            eventLogFactory,
            replayRunStore,
            new VoiceRuntimeOptions
            {
                DefaultVoice = "alloy",
                DefaultFormat = "mp3",
                MaxSynthesisChars = 12
            });

        await using var audio = new MemoryStream([1, 2, 3]);
        await service.RunAsync(
            new VoiceRunInput(
                ConversationId: "conv-voice-2",
                PersonaName: null,
                FileName: "sample.mp3",
                ContentType: "audio/mpeg",
                Length: audio.Length,
                Audio: audio,
                Voice: null,
                Format: null));

        Assert.NotNull(synthesis.LastRequest);
        Assert.Equal("alloy", synthesis.LastRequest!.Voice);
        Assert.Equal("mp3", synthesis.LastRequest.Format);
        Assert.Equal("AAAAAAAAAAAA...", synthesis.LastRequest.Input);
    }

    /// <summary>
    /// 模拟“文本 Runtime 已经结束，并向 replay 写出 run_completed”。
    /// 真正生产环境里这个事件来自 AgentRunContext；
    /// 测试里手工补出来，是为了验证语音链路如何续接后置事件 seq。
    /// </summary>
    private static async Task<VoiceAgentRuntimeResult> CompleteRuntimeAsync(
        VoiceAgentRuntimeRequest request,
        string finalOutput,
        long finalSeq,
        CancellationToken ct)
    {
        await request.EventSink.WriteAsync(
            new RunEvent(
                request.RunId,
                DateTimeOffset.UtcNow,
                finalSeq,
                "run_completed",
                JsonSerializer.SerializeToElement(new { finalOutput })),
            ct);

        return new VoiceAgentRuntimeResult(
            request.ConversationId,
            request.RunId,
            "completed",
            request.PersonaName,
            "voice_runtime",
            finalOutput,
            finalSeq);
    }

    /// <summary>
    /// `model_selected` 是 replay 里的关键审计事件，单独抽成断言帮助方法，
    /// 这样主测试用例读起来更像事件时间线，而不是一大串字段比较。
    /// </summary>
    private static void AssertModelSelected(RunEvent evt, string purpose, string model)
    {
        Assert.Equal("model_selected", evt.Type);
        Assert.Equal(purpose, evt.Payload.GetProperty("purpose").GetString());
        Assert.Equal(model, evt.Payload.GetProperty("model").GetString());
    }

    /// <summary>
    /// 文本 Runtime 的测试替身。
    /// 这里不测真实 AgentRuntimeService，只给 VoiceOrchestrationService 一个可控的最小返回值。
    /// </summary>
    private sealed class TestVoiceAgentRuntime : IVoiceAgentRuntime
    {
        private readonly Func<VoiceAgentRuntimeRequest, CancellationToken, Task<VoiceAgentRuntimeResult>> _handler;

        public TestVoiceAgentRuntime(Func<VoiceAgentRuntimeRequest, CancellationToken, Task<VoiceAgentRuntimeResult>> handler)
        {
            _handler = handler;
        }

        public Task<VoiceAgentRuntimeResult> RunAsync(VoiceAgentRuntimeRequest request, CancellationToken ct = default)
            => _handler(request, ct);
    }

    /// <summary>
    /// STT 测试替身：不关心真实音频内容，只返回预设文本。
    /// </summary>
    private sealed class TestTranscriptionService : IVoiceTranscriptionService
    {
        private readonly string _text;

        public TestTranscriptionService(string text)
        {
            _text = text;
        }

        public Task<VoiceTranscriptionResult> TranscribeAsync(VoiceTranscriptionRequest request, CancellationToken ct = default)
            => Task.FromResult(new VoiceTranscriptionResult(_text, "test-provider", request.Model));
    }

    /// <summary>
    /// TTS 测试替身：记录最后一次请求，便于断言默认 voice / format / 输入文本是否正确。
    /// </summary>
    private sealed class TestSynthesisService : IVoiceSynthesisService
    {
        public VoiceSynthesisRequest? LastRequest { get; private set; }

        public Task<VoiceSynthesisResult> SynthesizeAsync(VoiceSynthesisRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new VoiceSynthesisResult(
                AudioBytes: [9, 8, 7],
                ContentType: request.Format == "wav" ? "audio/wav" : "audio/mpeg",
                Format: request.Format,
                Provider: "test-provider",
                Model: request.Model));
        }
    }

    /// <summary>
    /// 模型路由测试替身：只为语音场景提供固定路由结果。
    /// </summary>
    private sealed class TestModelRouter : IModelRouter
    {
        public ModelSelection Select(ModelPurpose purpose)
            => purpose switch
            {
                ModelPurpose.VoiceStt => new ModelSelection(purpose, "test-provider", "test-stt-model", "voice_stt_test"),
                ModelPurpose.VoiceTts => new ModelSelection(purpose, "test-provider", "test-tts-model", "voice_tts_test"),
                _ => new ModelSelection(purpose, "test-provider", "unused", "unused")
            };
    }

    /// <summary>
    /// 捕获 replay 事件的内存 sink，便于按 seq 断言完整时间线。
    /// </summary>
    private sealed class CapturingRunEventSink : IRunEventSink
    {
        public List<RunEvent> Events { get; } = [];

        public ValueTask WriteAsync(RunEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 事件日志工厂测试替身：不真正写 JSONL 文件，只返回固定 path 与内存 sink。
    /// </summary>
    private sealed class TestRunEventLogFactory : IRunEventLogFactory
    {
        private readonly IRunEventSink _sink;
        private readonly string _path;

        public TestRunEventLogFactory(IRunEventSink sink, string path)
        {
            _sink = sink;
            _path = path;
        }

        public RunEventLogHandle CreateAgentRunLog(string runId) => new(_sink, _path);

        public RunEventLogHandle CreateDailySuggestionLog(DateOnly date) => new(_sink, _path);
    }

    /// <summary>
    /// replay run 元数据存储测试替身：用内存集合验证 SaveAsync 的结果。
    /// </summary>
    private sealed class TestReplayRunStore : IReplayRunStore
    {
        public List<ReplayRunRecord> Records { get; } = [];

        public Task SaveAsync(ReplayRunRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<ReplayRunRecord?> GetAsync(string runId, CancellationToken ct = default)
            => Task.FromResult<ReplayRunRecord?>(Records.FirstOrDefault(x => x.RunId == runId));

        public Task<IReadOnlyList<ReplayRunRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ReplayRunRecord>>(Records);
    }
}

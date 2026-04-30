using System.Text;
using System.Text.Json;
using SKAgent.Application.Voice;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Replay;
using SKAgent.Core.Voice;
using Xunit;

namespace SKAgent.Tests.Voice;

public sealed class VoiceRuntimeServiceTests
{
    [Fact]
    public async Task RunAsync_ShouldEmitVoiceEvents_AndPersistReplayMetadata()
    {
        var eventSink = new CapturingRunEventSink();
        var eventLogFactory = new TestRunEventLogFactory(eventSink, "voice-run.jsonl");
        var replayRunStore = new TestReplayRunStore();
        var runtime = new TestVoiceAgentRuntime((request, ct) =>
        {
            Assert.Equal(3, request.InitialEventSeq);

            return CompleteRuntimeAsync(
                request,
                "Voice runtime reply",
                finalSeq: 4,
                ct);
        });

        var service = new VoiceRuntimeService(
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
        var eventLogFactory = new TestRunEventLogFactory(new CapturingRunEventSink(), "voice-run-2.jsonl");
        var replayRunStore = new TestReplayRunStore();
        var synthesis = new TestSynthesisService();
        var longOutput = new string('A', 40);

        var service = new VoiceRuntimeService(
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

    private static void AssertModelSelected(RunEvent evt, string purpose, string model)
    {
        Assert.Equal("model_selected", evt.Type);
        Assert.Equal(purpose, evt.Payload.GetProperty("purpose").GetString());
        Assert.Equal(model, evt.Payload.GetProperty("model").GetString());
    }

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

    private sealed class CapturingRunEventSink : IRunEventSink
    {
        public List<RunEvent> Events { get; } = [];

        public ValueTask WriteAsync(RunEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return ValueTask.CompletedTask;
        }
    }

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

using System.Text.Json;
using SKAgent.Core.Observability;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Jobs;

internal sealed class BackgroundRunContext : IRunContext
{
    private readonly IRunEventSink _eventSink;
    private long _eventSeq;

    public BackgroundRunContext(
        string conversationId,
        string userInput,
        CancellationToken cancellationToken,
        IRunEventSink eventSink)
    {
        ConversationId = conversationId;
        UserInput = userInput;
        CancellationToken = cancellationToken;
        _eventSink = eventSink;
    }

    public string RunId { get; } = Guid.NewGuid().ToString("N");

    public string ConversationId { get; }

    public string UserInput { get; }

    public Dictionary<string, object> ConversationState { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CancellationToken CancellationToken { get; }

    public ValueTask EmitAsync(string type, object payload, CancellationToken ct = default)
    {
        var effectiveToken = ct == default ? CancellationToken : ct;
        effectiveToken.ThrowIfCancellationRequested();

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var element = doc.RootElement.Clone();

        return _eventSink.WriteAsync(new RunEvent(
            RunId,
            DateTimeOffset.UtcNow,
            Interlocked.Increment(ref _eventSeq),
            type,
            element), effectiveToken);
    }
}

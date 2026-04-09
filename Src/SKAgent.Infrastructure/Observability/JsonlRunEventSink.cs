using System.Text.Json;
using SKAgent.Core.Observability;

namespace SKAgent.Infrastructure.Observability;

public sealed class JsonlRunEventSink : IRunEventSink
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonlRunEventSink(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public string FilePath { get; }

    public async ValueTask WriteAsync(RunEvent evt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var envelope = new
        {
            runId = evt.RunId,
            seq = evt.Seq,
            ts = evt.TsUtc,
            type = evt.Type,
            payload = evt.Payload
        };

        var line = JsonSerializer.Serialize(envelope) + Environment.NewLine;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(FilePath, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}

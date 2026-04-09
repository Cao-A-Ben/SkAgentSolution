using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SKAgent.Application.Jobs;

namespace SKAgent.Host.HostedServices;

public sealed class DailySuggestionJob : BackgroundService
{
    private readonly DailySuggestionService _service;
    private readonly IOptions<DailySuggestionOptions> _options;
    private readonly ILogger<DailySuggestionJob> _logger;

    public DailySuggestionJob(
        DailySuggestionService service,
        IOptions<DailySuggestionOptions> options,
        ILogger<DailySuggestionJob> logger)
    {
        _service = service;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
            return;

        if (options.RunOnStartupIfMissing)
            await TryGenerateAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelay(options.TimeLocal);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            await TryGenerateAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task TryGenerateAsync(CancellationToken ct)
    {
        try
        {
            await _service.GenerateIfMissingAsync(ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailySuggestionJob failed.");
        }
    }

    private static TimeSpan ComputeDelay(string timeLocal)
    {
        if (!TimeOnly.TryParse(timeLocal, out var time))
            time = new TimeOnly(9, 0);

        var now = DateTimeOffset.Now;
        var next = now.Date.Add(time.ToTimeSpan());
        if (next <= now.LocalDateTime)
            next = next.AddDays(1);

        return next - now.LocalDateTime;
    }
}

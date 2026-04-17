using Microsoft.AspNetCore.Mvc;
using SKAgent.Application.Replay;
using SKAgent.Host.Contracts.Replay;

namespace SKAgent.Host.Controllers;

[ApiController]
[Route("api/replay")]
public sealed class ReplayController : ControllerBase
{
    private readonly ReplayQueryService _replayQueryService;

    public ReplayController(ReplayQueryService replayQueryService)
    {
        _replayQueryService = replayQueryService;
    }

    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns([FromQuery] int take = 30)
    {
        var runs = await _replayQueryService.ListRunsAsync(take, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(runs.Select(ToResponse));
    }

    [HttpGet("runs/{runId}")]
    public async Task<IActionResult> GetRun(string runId)
    {
        var detail = await _replayQueryService.GetRunAsync(runId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (detail is null)
            return NotFound(new { error = $"Replay run '{runId}' was not found." });

        return Ok(ToResponse(detail));
    }

    [HttpGet("runs/{runId}/events")]
    public async Task<IActionResult> GetRunEvents(string runId)
    {
        var events = await _replayQueryService.GetEventsAsync(runId, HttpContext.RequestAborted).ConfigureAwait(false);
        if (events.Count == 0)
            return NotFound(new { error = $"Replay events for run '{runId}' were not found." });

        return Ok(events.Select(ToResponse));
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> ListSuggestions([FromQuery] int take = 30)
    {
        var suggestions = await _replayQueryService.ListSuggestionsAsync(take, HttpContext.RequestAborted).ConfigureAwait(false);
        return Ok(suggestions.Select(ToResponse));
    }

    private static ReplayRunSummaryResponse ToResponse(ReplayRunSummary summary)
        => new()
        {
            RunId = summary.RunId,
            Kind = summary.Kind,
            ConversationId = summary.ConversationId,
            PersonaName = summary.PersonaName,
            Status = summary.Status,
            StartedAt = summary.StartedAt,
            FinishedAt = summary.FinishedAt,
            Goal = summary.Goal,
            InputPreview = summary.InputPreview,
            FinalOutputPreview = summary.FinalOutputPreview,
            EventCount = summary.EventCount
        };

    private static ReplayRunDetailResponse ToResponse(ReplayRunDetail detail)
        => new()
        {
            Summary = ToResponse(detail.Summary),
            Prompt = detail.Prompt is null
                ? null
                : new ReplayPromptResponse
                {
                    Target = detail.Prompt.Target,
                    Hash = detail.Prompt.Hash,
                    CharBudget = detail.Prompt.CharBudget,
                    LayersUsed = detail.Prompt.LayersUsed,
                    SystemChars = detail.Prompt.SystemChars,
                    UserChars = detail.Prompt.UserChars,
                    SystemText = detail.Prompt.SystemText,
                    UserText = detail.Prompt.UserText
                },
            Steps = detail.Steps.Select(x => new ReplayStepResponse
            {
                Order = x.Order,
                Kind = x.Kind,
                Target = x.Target,
                Status = x.Status,
                OutputPreview = x.OutputPreview,
                Error = x.Error
            }).ToList(),
            Memory = detail.Memory is null
                ? null
                : new ReplayMemoryResponse
                {
                    RecallSource = detail.Memory.RecallSource,
                    RecallPreview = detail.Memory.RecallPreview,
                    ByRouteCounts = detail.Memory.ByRouteCounts,
                    TotalItems = detail.Memory.TotalItems,
                    BudgetUsed = detail.Memory.BudgetUsed,
                    ConflictsResolved = detail.Memory.ConflictsResolved,
                    Layers = detail.Memory.Layers.Select(x => new ReplayMemoryLayerResponse
                    {
                        Layer = x.Layer,
                        CountBefore = x.CountBefore,
                        CountAfter = x.CountAfter,
                        BudgetChars = x.BudgetChars,
                        TruncateReason = x.TruncateReason
                    }).ToList(),
                    VectorTopK = detail.Memory.VectorTopK,
                    VectorLatencyMs = detail.Memory.VectorLatencyMs,
                    VectorScoreMin = detail.Memory.VectorScoreMin,
                    VectorScoreMax = detail.Memory.VectorScoreMax
                }
        };

    private static ReplayEventResponse ToResponse(ReplayEventEnvelope evt)
        => new()
        {
            RunId = evt.RunId,
            Seq = evt.Seq,
            Timestamp = evt.Timestamp,
            Type = evt.Type,
            Payload = evt.Payload
        };

    private static ReplaySuggestionResponse ToResponse(ReplaySuggestionSummary summary)
        => new()
        {
            Date = summary.Date,
            Suggestion = summary.Suggestion,
            RunId = summary.RunId,
            ConversationId = summary.ConversationId,
            PersonaName = summary.PersonaName,
            PromptHash = summary.PromptHash,
            ProfileHash = summary.ProfileHash,
            CreatedAtUtc = summary.CreatedAtUtc,
            EventLogPath = summary.EventLogPath,
            ReplayAvailable = summary.ReplayAvailable
        };
}

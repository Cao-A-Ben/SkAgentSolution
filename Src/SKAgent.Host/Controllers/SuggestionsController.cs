using Microsoft.AspNetCore.Mvc;
using SKAgent.Application.Jobs;
using SKAgent.Core.Suggestions;
using SKAgent.Host.Contracts.Suggestions;

namespace SKAgent.Host.Controllers;

[ApiController]
[Route("api/suggestions")]
public sealed class SuggestionsController : ControllerBase
{
    private readonly DailySuggestionService _dailySuggestionService;

    public SuggestionsController(DailySuggestionService dailySuggestionService)
    {
        _dailySuggestionService = dailySuggestionService;
    }

    [HttpPost("daily:run")]
    public async Task<IActionResult> RunDaily([FromBody] DailySuggestionRunRequest? request)
    {
        var ct = HttpContext.RequestAborted;
        DateOnly? date = null;

        if (!string.IsNullOrWhiteSpace(request?.Date))
        {
            if (!DateOnly.TryParse(request.Date, out var parsed))
                return BadRequest(new { error = "Invalid date. Use yyyy-MM-dd." });
            date = parsed;
        }

        try
        {
            var result = await _dailySuggestionService.GenerateIfMissingAsync(
                date,
                request?.PersonaName,
                request?.ConversationId,
                ct).ConfigureAwait(false);

            return Ok(ToResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 30)
    {
        var ct = HttpContext.RequestAborted;
        var records = await _dailySuggestionService.ListRecentAsync(take, ct).ConfigureAwait(false);
        return Ok(records.Select(x => ToResponse(new DailySuggestionResult(x, Created: false))));
    }

    private static DailySuggestionResponse ToResponse(DailySuggestionResult result)
        => new()
        {
            Date = result.Record.Date.ToString("yyyy-MM-dd"),
            Suggestion = result.Record.Suggestion,
            RunId = result.Record.RunId,
            ConversationId = result.Record.ConversationId,
            PersonaName = result.Record.PersonaName,
            PromptHash = result.Record.PromptHash,
            ProfileHash = result.Record.ProfileHash,
            EventLogPath = result.Record.EventLogPath,
            Created = result.Created
        };
}

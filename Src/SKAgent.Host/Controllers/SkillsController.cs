using Microsoft.AspNetCore.Mvc;
using SKAgent.Core.Skills;
using SKAgent.Host.Contracts.Skills;

namespace SKAgent.Host.Controllers;

[ApiController]
[Route("api/skills")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISkillRegistry _skillRegistry;

    public SkillsController(ISkillRegistry skillRegistry)
    {
        _skillRegistry = skillRegistry;
    }

    [HttpGet]
    public IActionResult List()
    {
        var items = _skillRegistry.List()
            .Select(skill => new SkillSummaryResponse
            {
                Name = skill.Name,
                DisplayName = skill.DisplayName,
                Description = skill.Description,
                RecommendedTools = skill.RecommendedTools?.ToArray() ?? []
            })
            .ToList();

        return Ok(items);
    }
}

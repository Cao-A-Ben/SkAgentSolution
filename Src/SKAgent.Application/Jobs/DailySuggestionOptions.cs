namespace SKAgent.Application.Jobs;

public sealed class DailySuggestionOptions
{
    public const string SectionName = "DailySuggestion";

    public bool Enabled { get; set; }

    public bool RunOnStartupIfMissing { get; set; } = true;

    public bool UseLatestConversation { get; set; } = true;

    public string TimeLocal { get; set; } = "09:00";

    public string PersonaName { get; set; } = "default";

    public string? ConversationId { get; set; }

    public int CharBudget { get; set; } = 6000;

    public int RecentTurnTake { get; set; } = 4;
}

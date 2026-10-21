namespace FVN_REGISTER.AI.Application.Contracts;

public sealed record CreateAIConversationRequest(string? Title);
public sealed record AIConversationAskRequest(Guid ConversationId, string Query, System.Text.Json.JsonElement BusinessContext, int RetrievalLimit = 8);

public sealed class AIConversationSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AIConversationTranscriptDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<AIConversationMessageDto> Messages { get; set; } = [];
}

public sealed class AIConversationMessageDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string Status { get; set; } = "";
    public string? CitationsJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

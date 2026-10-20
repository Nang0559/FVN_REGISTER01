namespace FVN_REGISTER.AI.Application.Contracts;

public sealed record CreateAIConversationRequest(string? Title);
public sealed record AIConversationAskRequest(Guid ConversationId, string Query, System.Text.Json.JsonElement BusinessContext, int RetrievalLimit = 8);

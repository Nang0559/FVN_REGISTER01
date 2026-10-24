namespace FVN_REGISTER.AI.Application.Policies;

/// <summary>
/// Defines which RAG outcomes may become reusable conversation context.
/// Access-denied and invalid model responses remain visible as diagnostic outcomes,
/// but must never be replayed to a later model turn as successful assistant context.
/// </summary>
public static class ConversationTurnStatusPolicy
{
    public static bool IsReusableAssistantResponse(string? status) =>
        string.Equals(status, "Supported", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "PartiallySupported", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "NotFound", StringComparison.OrdinalIgnoreCase);

    public static string GetUserTurnStatus(string? assistantStatus) =>
        IsReusableAssistantResponse(assistantStatus) ? "Completed" : "Abstained";
}

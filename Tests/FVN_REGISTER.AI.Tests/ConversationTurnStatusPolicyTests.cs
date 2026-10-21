using FVN_REGISTER.AI.Application.Policies;

namespace FVN_REGISTER.AI.Tests;

public sealed class ConversationTurnStatusPolicyTests
{
    [Theory]
    [InlineData("Supported", true)]
    [InlineData("PartiallySupported", true)]
    [InlineData("NotFound", true)]
    [InlineData("Forbidden", false)]
    [InlineData("InvalidModelResponse", false)]
    [InlineData("ProviderUnavailable", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AssistantResponse_IsReusableOnlyForExplicitRagOutcomes(string? status, bool expected)
    {
        Assert.Equal(expected, ConversationTurnStatusPolicy.IsReusableAssistantResponse(status));
    }

    [Theory]
    [InlineData("Supported", "Completed")]
    [InlineData("PartiallySupported", "Completed")]
    [InlineData("NotFound", "Completed")]
    [InlineData("Forbidden", "Abstained")]
    [InlineData("InvalidModelResponse", "Abstained")]
    [InlineData("UnknownStatus", "Abstained")]
    public void UserTurn_IsCompletedOnlyWhenAssistantOutcomeIsReusable(string status, string expected)
    {
        Assert.Equal(expected, ConversationTurnStatusPolicy.GetUserTurnStatus(status));
    }
}

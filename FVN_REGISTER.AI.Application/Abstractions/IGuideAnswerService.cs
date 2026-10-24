using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>
/// Answers a how-to question from user-guide passages supplied by the trusted host. Unlike the knowledge-base RAG there is no
/// document upload, approval or vector store: the guide is the Markdown that ships with the application.
/// </summary>
public interface IGuideAnswerService
{
    Task<GuideAnswerResult> AnswerAsync(GuideAnswerRequest request, CancellationToken cancellationToken = default);
}

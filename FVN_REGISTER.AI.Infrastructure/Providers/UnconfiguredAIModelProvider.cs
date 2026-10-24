using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
namespace FVN_REGISTER.AI.Infrastructure.Providers;

/// <summary>Safe default: no fabricated AI output when a real provider has not been configured.</summary>
public sealed class UnconfiguredAIModelProvider : IAIModelProvider, IAIModelProvenance
{
    public string ProviderName=>"Unconfigured";
    public string ModelVersion=>"Unconfigured";
    public Task<AIModelResponse> GenerateAsync(AIModelRequest request,CancellationToken cancellationToken=default)
        => throw new InvalidOperationException("No AI model provider is configured. Configure an approved local or hosted provider before processing.");
}
namespace FVN_REGISTER.Application.Interfaces.Security;

public sealed record EndpointCredentialProvisionDto(
    string DeviceKey,
    string? ComputerName,
    int? EquipmentAssetId);

public sealed record EndpointCredentialProvisionResult(
    string DeviceKey,
    string ApiKey,
    DateTimeOffset ExpiresAtUtc);

public sealed record EndpointCredentialStatusDto(
    string DeviceKey,
    bool EndpointExists,
    bool HasActiveCredential,
    DateTimeOffset? ExpiresAtUtc);

public interface IEndpointCredentialService
{
    Task<EndpointCredentialStatusDto> GetStatusAsync(
        string deviceKey,
        CancellationToken cancellationToken = default);

    Task<EndpointCredentialProvisionResult> ProvisionAsync(
        EndpointCredentialProvisionDto request,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string deviceKey,
        int actorUserId,
        CancellationToken cancellationToken = default);
}

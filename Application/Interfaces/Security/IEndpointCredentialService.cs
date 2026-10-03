namespace FVN_REGISTER.Application.Interfaces.Security;

public sealed record EndpointCredentialProvisionDto(
    string DeviceKey,
    string? ComputerName,
    int? EquipmentAssetId);

public sealed record EndpointCredentialProvisionResult(
    string DeviceKey,
    string ApiKey,
    DateTimeOffset ExpiresAtUtc);

public sealed record EndpointCredentialHistoryDto(
    long CredentialId,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    int? CreatedBy,
    int? RevokedBy,
    string? LastAction);

public sealed record EndpointCredentialStatusDto(
    string DeviceKey,
    bool EndpointExists,
    bool HasActiveCredential,
    DateTimeOffset? ExpiresAtUtc,
    long? EndpointId = null,
    int? EquipmentAssetId = null,
    string? ComputerName = null,
    string? SerialNumber = null,
    string? OsName = null,
    string? OsVersion = null,
    string? AgentVersion = null,
    string? AgentInstallationId = null,
    string? AgentStatus = null,
    DateTime? LastSeenUtc = null);

public interface IEndpointCredentialService
{
    Task<EndpointCredentialStatusDto> GetStatusAsync(
        string deviceKey,
        CancellationToken cancellationToken = default);

    Task<EndpointCredentialProvisionResult> ProvisionAsync(
        EndpointCredentialProvisionDto request,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<EndpointCredentialStatusDto> GetStatusByEquipmentAsync(int equipmentAssetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EndpointCredentialHistoryDto>> GetHistoryByEquipmentAsync(int equipmentAssetId, CancellationToken cancellationToken = default);
    Task<EndpointCredentialProvisionResult> ProvisionForEquipmentAsync(int equipmentAssetId, int actorUserId, CancellationToken cancellationToken = default);

    Task<EndpointCredentialProvisionResult?> RotateWithCurrentApiKeyAsync(
        string currentApiKey,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string deviceKey,
        int actorUserId,
        CancellationToken cancellationToken = default);
}

namespace FVN_REGISTER.Contract.Dtos.Security;

public sealed record LanscopeClientInventoryDto(
    string? ClientId, string? ComputerName, string? Ip, string? Mac, string? SerialNumber,
    string? WindowsUser, string? Domain, string? Ou, string? Group, string? Os,
    string? Manufacturer, string? Model);

public sealed record LanscopeEnrollmentRequestDto(
    int DeploymentId, int TargetId, string EnrollmentToken,
    string? ClientId, string? ComputerName, string? Ip, string? Mac, string? SerialNumber,
    string? WindowsUser, string? Domain, string? Ou, string? Group, string? Os,
    string? Manufacturer, string? Model, string? HardwareUuid, string? AgentInstallationId,
    string? AgentVersion);

public sealed record LanscopeEnrollmentResponseDto(
    string DeviceKey, string ApiKey, DateTimeOffset ExpiresAtUtc, string IdentityStatus);

public sealed record LanscopeDeploymentCreateRequest(
    string DeploymentCode, string? PackageVersion, DateTimeOffset? ExpiresAtUtc);

public sealed record LanscopeDeploymentTargetRequest(
    string TargetId, LanscopeClientInventoryDto Client);

public sealed record LanscopeDeploymentTargetResult(
    int TargetId, string TargetKey, string EnrollmentToken, string BootstrapFileName, string BootstrapJson);

public sealed record LanscopeDeploymentTargetViewDto(
    int TargetId, string TargetKey, string? ClientId, string? ComputerName, string? SerialNumber,
    string Status, DateTimeOffset? TokenExpiresAtUtc, DateTimeOffset? EnrolledAtUtc, long? EndpointDeviceId);

public sealed record LanscopeDeploymentViewDto(
    int DeploymentId, string DeploymentCode, string? PackageVersion, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ExpiresAtUtc, int TargetCount, int EnrolledCount);

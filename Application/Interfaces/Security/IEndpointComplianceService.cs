namespace FVN_REGISTER.Application.Interfaces.Security;

public interface IEndpointComplianceService
{
    /// <summary>
    /// Evaluates the latest trusted inventory of one endpoint against the currently
    /// published Endpoint Governance policies. This is an internal server-side
    /// operation invoked after authenticated agent ingestion or by an authorized
    /// compliance action; it is not an alternate permission system.
    /// </summary>
    Task<int> EvaluateAsync(long endpointDeviceId, CancellationToken cancellationToken = default);
}

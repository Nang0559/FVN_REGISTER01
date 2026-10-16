using FVN_REGISTER.Contract.Dtos.Security;

namespace FVN_REGISTER.Application.Interfaces.Security;

public interface IEndpointInventoryService
{
    Task<EndpointInventorySummaryDto> UpsertInventoryAsync(
        EndpointInventoryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<EndpointInventorySummaryDto?> GetAsync(
        string deviceKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EndpointInventorySummaryDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}

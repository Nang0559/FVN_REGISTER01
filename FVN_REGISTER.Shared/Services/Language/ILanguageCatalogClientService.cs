using FVN_REGISTER.Contract.Dtos.Language;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Language;

/// <summary>Public (no login) read of the texts saved in the Language Center; the login page is translated too.</summary>
public interface ILanguageCatalogClientService
{
    Task<ApiResponse<LocalizationRuntimeSnapshotDto>> GetRuntimeSnapshotAsync(long? sinceVersion, CancellationToken ct = default);
}

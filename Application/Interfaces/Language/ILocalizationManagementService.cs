using FVN_REGISTER.Contract.Dtos.Language;

namespace FVN_REGISTER.Application.Interfaces.Language;

public interface ILocalizationManagementService
{
    Task<LocalizationCatalogDto> GetCatalogAsync(CancellationToken ct = default);
    Task UpsertAsync(LocalizationUpsertRequest request, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<LocalizationAuditResultDto> AuditAsync(CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> ExportAsync(CancellationToken ct = default);
    Task<LocalizationImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default);
}

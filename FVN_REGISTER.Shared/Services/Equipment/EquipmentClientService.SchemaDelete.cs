using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Equipment;

public sealed partial class EquipmentClientService
{
    public Task<ApiResponse<bool>> DeleteSchemaAsync(int schemaId, CancellationToken ct = default)
        => Delete<bool>($"api/equipment/schemas/{schemaId}", "delete schema", ct);

    private async Task<ApiResponse<T>> Delete<T>(string url, string op, CancellationToken ct)
    {
        try
        {
            return await _http.DeleteAsync<T>(url, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(true, ex, "[EQUIPMENT_CLIENT] {Op}", op);
            return ApiResponse<T>.Fail("Không thể thực hiện thao tác thiết bị.");
        }
    }
}

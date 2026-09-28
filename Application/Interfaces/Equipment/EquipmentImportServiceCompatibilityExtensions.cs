using FVN_REGISTER.Contract.Dtos.EquipmentImport;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

public static class EquipmentImportServiceCompatibilityExtensions
{
    public static Task<EquipmentImportBatchDto> StageExcelAsync(
        this IEquipmentImportService service,
        string deptCode,
        string fileName,
        Stream content,
        bool assignToEmployee = false,
        CancellationToken ct = default)
        => service.StageExcelAsync(deptCode, null, fileName, content, assignToEmployee, ct);
}

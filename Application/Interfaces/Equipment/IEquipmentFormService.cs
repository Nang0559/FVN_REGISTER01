using FVN_REGISTER.Contract.Dtos.EquipmentForms;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

public interface IEquipmentFormService
{
    Task<ServiceResult<List<EquipmentApplicableFormDto>>> GetMyApplicableFormsAsync(int assetId, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentFormAssignmentDto>>> GetAssignmentsAsync(string equipmentSchemaKey, CancellationToken ct = default);
    Task<ServiceResult<EquipmentFormAssignmentDto>> AssignAsync(EquipmentFormAssignmentRequest request, CancellationToken ct = default);
    Task<ServiceResult<bool>> RemoveAssignmentAsync(int assignmentId, CancellationToken ct = default);
    Task<ServiceResult<EquipmentFormSubmissionDto>> SubmitAsync(EquipmentFormSubmissionRequest request, CancellationToken ct = default);
}

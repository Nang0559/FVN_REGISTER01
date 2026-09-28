using FVN_REGISTER.Contract.Dtos.EquipmentForms;
using FVN_REGISTER.Contract.Dtos.PublicForms;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Equipment;

public sealed partial class EquipmentClientService
{
    public Task<ApiResponse<List<EquipmentApplicableFormDto>>> GetApplicableFormsAsync(int assetId, CancellationToken ct = default)
        => Get<List<EquipmentApplicableFormDto>>($"api/equipment/forms/my/{assetId}", "equipment request forms", ct);

    public Task<ApiResponse<List<EquipmentFormAssignmentDto>>> GetEquipmentFormAssignmentsAsync(string equipmentSchemaKey, CancellationToken ct = default)
        => Get<List<EquipmentFormAssignmentDto>>($"api/equipment/forms/assignments/{Uri.EscapeDataString(equipmentSchemaKey)}", "equipment form assignments", ct);

    public Task<ApiResponse<List<PublicFormDto>>> GetEquipmentFormCatalogAsync(CancellationToken ct = default)
        => Get<List<PublicFormDto>>("api/equipment/forms/catalog", "equipment form catalog", ct);

    public Task<ApiResponse<EquipmentFormAssignmentDto>> AssignEquipmentFormAsync(EquipmentFormAssignmentRequest request, CancellationToken ct = default)
        => Post<EquipmentFormAssignmentDto>("api/equipment/forms/assignments", request, "assign equipment form", ct);

    public Task<ApiResponse<bool>> RemoveEquipmentFormAssignmentAsync(int assignmentId, CancellationToken ct = default)
        => Post<bool>($"api/equipment/forms/assignments/{assignmentId}/remove", new { }, "remove equipment form assignment", ct);

    public Task<ApiResponse<EquipmentFormSubmissionDto>> SubmitEquipmentFormAsync(EquipmentFormSubmissionRequest request, CancellationToken ct = default)
        => Post<EquipmentFormSubmissionDto>("api/equipment/forms/submit", request, "submit equipment form", ct);
}

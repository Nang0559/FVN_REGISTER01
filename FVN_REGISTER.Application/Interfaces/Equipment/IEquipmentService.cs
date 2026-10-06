using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

public interface IEquipmentService
{
    Task<ServiceResult<bool>> HasModuleAccessAsync(CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentHandoverEmployeeOptionDto>>> GetAssignmentEmployeesAsync(int? deptCode = null, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentApproverDto>>> GetApproversAsync(int deptCode, CancellationToken ct = default);
    Task<ServiceResult<EquipmentRequestDto>> CreateRegistrationDraftAsync(CreateEquipmentRegistrationDto request, CancellationToken ct = default);
    Task<ServiceResult<EquipmentRequestDto>> SubmitRegistrationAsync(int requestId, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentRequestDto>>> GetMineAsync(CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentAssetDto>>> GetMyAssignedAssetsAsync(CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentAssetDto>>> GetAssetsAsync(int? deptCode = null, CancellationToken ct = default);
    Task<ServiceResult<EquipmentAssetDto>> SetEndpointAgentEligibilityAsync(int assetId, EndpointAgentEligibilityRequest request, CancellationToken ct = default);
    Task<ServiceResult<EquipmentRequestDto>> CreateRepairDraftAsync(CreateEquipmentRepairDto request, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentHandoverEmployeeOptionDto>>> GetRepairAssigneesAsync(int? deptCode = null, CancellationToken ct = default);
    Task<ServiceResult<EquipmentRequestDto>> SubmitRepairAsync(int requestId, CancellationToken ct = default);
    Task<ServiceResult<EquipmentRequestDto>> CompleteRepairAsync(int requestId, string feedback, CancellationToken ct = default);
    Task<ServiceResult<EquipmentAssetDto>> ScanAsync(string qrToken, CancellationToken ct = default);
    Task<ServiceResult<EquipmentAssetDto>> GetAssetAsync(int assetId, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentReportRowDto>>> GetReportAsync(EquipmentReportFilterDto filter, CancellationToken ct = default);
    Task<ServiceResult<byte[]>> ExportReportAsync(EquipmentReportFilterDto filter, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentHandoverEmployeeOptionDto>>> GetHandoverEmployeesAsync(int? deptCode = null, CancellationToken ct = default);
    Task<ServiceResult<List<EquipmentHandoverCandidateDto>>> GetHandoverCandidatesAsync(string oldEmployeeCode, int? deptCode = null, CancellationToken ct = default);
    Task<ServiceResult<EquipmentHandoverResultDto>> HandoverAsync(EquipmentHandoverRequest request, CancellationToken ct = default);
}

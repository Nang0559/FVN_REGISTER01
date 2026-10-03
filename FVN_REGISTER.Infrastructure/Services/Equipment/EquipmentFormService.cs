using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.EquipmentForms;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Equipment;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.PublicForms;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Application.Interfaces.Orchestrators;

namespace FVN_REGISTER.Infrastructure.Services.Equipment;

public sealed class EquipmentFormService : IEquipmentFormService
{
    private const string FormAssignmentResourceType = "EQUIPMENT_SCHEMA";

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IExcelPlatform _excel;
    private readonly IFeatureOperatorAssignmentService _featureOperators;
    private readonly IApprovalWorkflowOrchestrator<EquipmentRequestSubject> _workflow;
    private readonly IApprovalSelectionService _approvalSelections;

    public EquipmentFormService(IUnitOfWork uow, ICurrentUserService currentUser, IAuthorizationService authorization, IExcelPlatform excel, IFeatureOperatorAssignmentService featureOperators, IApprovalWorkflowOrchestrator<EquipmentRequestSubject> workflow, IApprovalSelectionService approvalSelections)
    {
        _uow = uow;
        _currentUser = currentUser;
        _authorization = authorization;
        _excel = excel;
        _featureOperators = featureOperators;
        _workflow = workflow;
        _approvalSelections = approvalSelections;
    }

    public async Task<ServiceResult<List<EquipmentApplicableFormDto>>> GetMyApplicableFormsAsync(int assetId, CancellationToken ct = default)
    {
        var user = RequireUser();
        var asset = await GetAssignedAssetAsync(assetId, user.EmployeeCode, ct);
        if (asset == null) return ServiceResult<List<EquipmentApplicableFormDto>>.Fail("Thiết bị không tồn tại hoặc không được giao cho bạn.");
        if (string.IsNullOrWhiteSpace(asset.EquipmentSchemaKey)) return ServiceResult<List<EquipmentApplicableFormDto>>.Ok(new());

        var now = DateTime.Now;
        var rows = await _uow.Repository<F03EquipmentFormAssignment>().Query().AsNoTracking()
            .Where(x => x.IsActiveAssignment && x.EquipmentSchemaKey == asset.EquipmentSchemaKey)
            .Join(_uow.Repository<F03PublicForm>().Query().AsNoTracking()
                    .Where(x => x.IsActive == true && x.Status == "Published" && (x.StartAt == null || x.StartAt <= now) && (x.EndAt == null || x.EndAt >= now)),
                a => a.FormId, f => f.Id,
                (a, f) => new EquipmentApplicableFormDto
                {
                    FormId = f.Id, FormCode = f.FormCode, Title = f.Title, Description = f.Description,
                    CategoryCode = f.CategoryCode, Version = f.Version, RequireApproval = f.RequireApproval,
                    DisplayOrder = a.DisplayOrder, AssetId = asset.Id, EquipmentCode = asset.EquipmentCode, EquipmentName = asset.EquipmentName
                })
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Title).ToListAsync(ct);

        var formIds = rows.Select(x => x.FormId).Distinct().ToList();
        if (formIds.Count > 0)
        {
            var questions = await _uow.Repository<F03PublicFormQuestion>().Query().AsNoTracking()
                .Include(x => x.Options).Where(x => formIds.Contains(x.FormId)).OrderBy(x => x.Sequence).ToListAsync(ct);
            foreach (var row in rows)
            {
                row.Questions = questions.Where(q => q.FormId == row.FormId && q.IsActive == true).OrderBy(q => q.Sequence)
                    .Select(q => new EquipmentFormQuestionDto
                    {
                        Id = q.Id, QuestionCode = q.QuestionCode, QuestionText = q.QuestionText, QuestionType = q.QuestionType,
                        HelpText = q.HelpText, Placeholder = q.Placeholder, IsRequired = q.IsRequired, Sequence = q.Sequence,
                        Options = q.Options.Where(o => o.IsActive == true).OrderBy(o => o.Sequence)
                            .Select(o => new EquipmentFormQuestionOptionDto { OptionCode = o.OptionCode, OptionText = o.OptionText }).ToList()
                    }).ToList();
            }
        }
        return ServiceResult<List<EquipmentApplicableFormDto>>.Ok(rows);
    }

    public async Task<ServiceResult<List<EquipmentFormAssignmentDto>>> GetAssignmentsAsync(string equipmentSchemaKey, CancellationToken ct = default)
    {
        var user = RequireUser();
        var key = NormalizeKey(equipmentSchemaKey);
        if (string.IsNullOrWhiteSpace(key)) return ServiceResult<List<EquipmentFormAssignmentDto>>.Fail("Mã loại thiết bị là bắt buộc.");
        if (!await EnsureFormManagementAccessAsync(user, key, ct)) return ServiceResult<List<EquipmentFormAssignmentDto>>.Fail("Bạn không có quyền quản lý biểu mẫu của loại thiết bị này.");

        var rows = await _uow.Repository<F03EquipmentFormAssignment>().Query().AsNoTracking().Where(x => x.EquipmentSchemaKey == key)
            .Join(_uow.Repository<F03PublicForm>().Query().AsNoTracking(), a => a.FormId, f => f.Id,
                (a, f) => new EquipmentFormAssignmentDto { Id = a.Id, EquipmentSchemaKey = a.EquipmentSchemaKey, FormId = f.Id, FormCode = f.FormCode, FormTitle = f.Title, DisplayOrder = a.DisplayOrder, IsActiveAssignment = a.IsActiveAssignment })
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.FormTitle).ToListAsync(ct);
        return ServiceResult<List<EquipmentFormAssignmentDto>>.Ok(rows);
    }

    public async Task<ServiceResult<EquipmentFormAssignmentDto>> AssignAsync(EquipmentFormAssignmentRequest request, CancellationToken ct = default)
    {
        var user = RequireUser();
        var key = NormalizeKey(request.EquipmentSchemaKey);
        if (string.IsNullOrWhiteSpace(key) || request.FormId <= 0) return ServiceResult<EquipmentFormAssignmentDto>.Fail("Loại thiết bị và biểu mẫu là bắt buộc.");
        if (!await EnsureFormManagementAccessAsync(user, key, ct)) return ServiceResult<EquipmentFormAssignmentDto>.Fail("Bạn không có quyền quản lý biểu mẫu của loại thiết bị này.");

        var form = await _uow.Repository<F03PublicForm>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.FormId && x.IsActive == true, ct);
        if (form == null) return ServiceResult<EquipmentFormAssignmentDto>.Fail("Không tìm thấy biểu mẫu.");
        var existing = await _uow.Repository<F03EquipmentFormAssignment>().Query().FirstOrDefaultAsync(x => x.EquipmentSchemaKey == key && x.FormId == request.FormId, ct);
        if (existing == null)
        {
            existing = new F03EquipmentFormAssignment { EquipmentSchemaKey = key, FormId = request.FormId, DisplayOrder = request.DisplayOrder, IsActiveAssignment = true, CreatedBy = user.UserId };
            await _uow.Repository<F03EquipmentFormAssignment>().AddAsync(existing, ct);
        }
        else
        {
            existing.DisplayOrder = request.DisplayOrder; existing.IsActiveAssignment = true; existing.ModifiedBy = user.UserId; existing.ModifiedAt = DateTime.Now;
        }
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<EquipmentFormAssignmentDto>.Ok(new EquipmentFormAssignmentDto { Id = existing.Id, EquipmentSchemaKey = key, FormId = form.Id, FormCode = form.FormCode, FormTitle = form.Title, DisplayOrder = existing.DisplayOrder, IsActiveAssignment = true });
    }

    public async Task<ServiceResult<bool>> RemoveAssignmentAsync(int assignmentId, CancellationToken ct = default)
    {
        var user = RequireUser();
        var assignment = await _uow.Repository<F03EquipmentFormAssignment>().Query().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment == null) return ServiceResult<bool>.Fail("Không tìm thấy cấu hình biểu mẫu.");
        if (!await EnsureFormManagementAccessAsync(user, assignment.EquipmentSchemaKey, ct)) return ServiceResult<bool>.Fail("Bạn không có quyền quản lý biểu mẫu của loại thiết bị này.");
        assignment.IsActiveAssignment = false; assignment.ModifiedBy = user.UserId; assignment.ModifiedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true, "Đã ngừng áp dụng biểu mẫu cho loại thiết bị.");
    }

    public async Task<ServiceResult<EquipmentFormSubmissionDto>> SubmitAsync(EquipmentFormSubmissionRequest request, CancellationToken ct = default)
    {
        var user = RequireUser();
        var asset = await GetAssignedAssetAsync(request.AssetId, user.EmployeeCode, ct);
        if (asset == null) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Thiết bị không tồn tại hoặc không được giao cho bạn.");
        if (request.FormId <= 0) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Biểu mẫu là bắt buộc.");
        if (string.IsNullOrWhiteSpace(asset.EquipmentSchemaKey)) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Thiết bị chưa được xác định loại/mẫu dữ liệu để áp dụng biểu mẫu.");
        var assigned = await _uow.Repository<F03EquipmentFormAssignment>().Query().AsNoTracking().AnyAsync(x => x.IsActiveAssignment && x.EquipmentSchemaKey == asset.EquipmentSchemaKey && x.FormId == request.FormId, ct);
        if (!assigned) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Biểu mẫu này không được áp dụng cho loại thiết bị của bạn.");
        var form = await _uow.Repository<F03PublicForm>().Query().Include(x => x.Questions).AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.FormId && x.IsActive == true && x.Status == "Published", ct);
        if (form == null) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Biểu mẫu chưa được phát hành hoặc không còn hoạt động.");
        ValidateRequiredAnswers(form, request.Answers);

        var submission = new F03PublicFormSubmission { FormId = form.Id, EmployeeCode = user.EmployeeCode ?? string.Empty, EquipmentAssetId = asset.Id, SubmittedAt = DateTime.Now, Status = form.RequireApproval ? "PendingApproval" : "Submitted", FormVersion = form.Version, CreatedBy = user.UserId };
        await _uow.Repository<F03PublicFormSubmission>().AddAsync(submission, ct); await _uow.SaveChangesAsync(ct);
        foreach (var answer in request.Answers.GroupBy(x => x.QuestionId).Select(x => x.Last()))
        {
            if (!form.Questions.Any(q => q.Id == answer.QuestionId)) continue;
            await _uow.Repository<F03PublicFormAnswer>().AddAsync(new F03PublicFormAnswer { SubmissionId = submission.Id, QuestionId = answer.QuestionId, TextValue = answer.TextValue, NumberValue = answer.NumberValue, DateValue = answer.DateValue, BoolValue = answer.BoolValue, JsonValue = answer.JsonValue, CreatedBy = user.UserId }, ct);
        }
        await _uow.SaveChangesAsync(ct);

        var result = new EquipmentFormSubmissionDto { SubmissionId = submission.Id, AssetId = asset.Id, FormId = form.Id, FormCode = form.FormCode, Status = submission.Status, RequireApproval = form.RequireApproval };
        if (!form.RequireApproval) return ServiceResult<EquipmentFormSubmissionDto>.Ok(result, "Đã gửi biểu mẫu.");

        if (request.ApprovalSelections == null || request.ApprovalSelections.Count == 0)
            return ServiceResult<EquipmentFormSubmissionDto>.Fail("Vui lòng chọn người phê duyệt cho từng cấp.");

        var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => x.EmployeeCode == user.EmployeeCode && x.IsActive == true && x.EndWorkingDate == null)
            .Select(x => new { x.EmployeeCode, x.DeptCode, x.PositionCode }).FirstOrDefaultAsync(ct);
        if (employee == null || string.IsNullOrWhiteSpace(employee.DeptCode) || string.IsNullOrWhiteSpace(employee.PositionCode)) return ServiceResult<EquipmentFormSubmissionDto>.Fail("Không xác định được bộ phận/chức vụ của người yêu cầu để khởi tạo phê duyệt.");

        var equipmentRequest = new F03EquipmentRequest
        {
            RequestKind = EquipmentRequestKind.Form, AssetId = asset.Id, FormId = form.Id, FormSubmissionId = submission.Id, FormCode = form.FormCode,
            EmployeeCode = employee.EmployeeCode, DeptCode = employee.DeptCode, RequestStatus = ApprovalStatus.Pending, CreatedBy = user.UserId,
            OperatorUserId = user.UserId, ResponsibleEmployeeCode = asset.ResponsibleEmployeeCode, SelectedApproverCode = string.Empty, QrToken = asset.QrToken,
            EquipmentName = asset.EquipmentName, Specification = asset.Specification, SerialNumber = asset.SerialNumber, AssetCode = asset.AssetCode,
            PurchasePrice = asset.PurchasePrice, PurchaseDate = asset.PurchaseDate, ExpectedDepreciationDate = asset.ExpectedDepreciationDate,
            Location = asset.Location, Note = $"Biểu mẫu thiết bị {form.FormCode} - Submission #{submission.Id}"
        };
        await _uow.Repository<F03EquipmentRequest>().AddAsync(equipmentRequest, ct); await _uow.SaveChangesAsync(ct);
        await _approvalSelections.ReplaceAsync(RequestModule.Equipment, equipmentRequest.Id, request.ApprovalSelections, user.UserId, ct);
        var approval = await _workflow.InitApprovalAsync(equipmentRequest.Id, ApprovalBuildContext.ForEquipment(equipmentRequest.Id, employee.EmployeeCode, employee.DeptCode, employee.PositionCode), ct);
        if (!approval.IsSuccess)
        {
            equipmentRequest.RequestStatus = ApprovalStatus.Draft; submission.Status = "ApprovalInitFailed"; await _uow.SaveChangesAsync(ct);
            return ServiceResult<EquipmentFormSubmissionDto>.Fail(approval.Message ?? "Không thể khởi tạo luồng phê duyệt biểu mẫu.");
        }
        result.EquipmentRequestId = equipmentRequest.Id;
        return ServiceResult<EquipmentFormSubmissionDto>.Ok(result, "Đã gửi biểu mẫu vào quy trình phê duyệt.");
    }

    private async Task<bool> EnsureFormManagementAccessAsync(UserIdentityDto user, string equipmentSchemaKey, CancellationToken ct)
    {
        if (!await _authorization.HasAsync(user, SecurityFunctionCodes.EquipmentFormManage, ct)) return false;
        if (user.PermissionCode == UserPermissionCodes.SuperAdmin) return true;

        var schema = (await _excel.GetSchemasAsync("EQUIPMENT", "EQUIPMENT_ASSET", false, ct))
            .FirstOrDefault(x => x.SchemaKey.Equals(equipmentSchemaKey, StringComparison.OrdinalIgnoreCase)
                && x.Status == FVN_REGISTER.Core.Excel.ExcelSchemaStatus.Active);
        if (schema == null) return false;
        return await _featureOperators.CanOperateAsync(user.UserId, user.EmployeeCode, SecurityFunctionCodes.EquipmentFormManage, FormAssignmentResourceType, schema.Id, ct);
    }

    private async Task<F03EquipmentAsset?> GetAssignedAssetAsync(int assetId, string? employeeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) return null;
        return await _uow.Repository<F03EquipmentAsset>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == assetId && x.IsActive == true && (x.ResponsibleEmployeeCode == employeeCode || x.OperatingResponsibleEmployeeCode == employeeCode), ct);
    }

    private static void ValidateRequiredAnswers(F03PublicForm form, IReadOnlyCollection<EquipmentFormAnswerRequest> answers)
    {
        var supplied = answers.GroupBy(x => x.QuestionId).ToDictionary(g => g.Key, g => g.Last());
        var missing = form.Questions.Where(q => q.IsRequired).Where(q => !supplied.TryGetValue(q.Id, out var answer) || !HasValue(answer)).Select(q => q.QuestionText).ToList();
        if (missing.Count > 0) throw new ArgumentException($"Vui lòng hoàn thành: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "..." : string.Empty)}");
    }

    private static bool HasValue(EquipmentFormAnswerRequest answer) => answer.TextValue != null || answer.NumberValue.HasValue || answer.DateValue.HasValue || answer.BoolValue.HasValue || !string.IsNullOrWhiteSpace(answer.JsonValue);
    private static string NormalizeKey(string value) => (value ?? string.Empty).Trim();
    private UserIdentityDto RequireUser() => _currentUser.GetCurrentUser() ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
}

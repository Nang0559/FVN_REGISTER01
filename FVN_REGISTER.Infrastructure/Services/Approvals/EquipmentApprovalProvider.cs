using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Emails;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Equipment;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.PublicForms;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

public sealed class EquipmentApprovalProvider
    : BaseApprovalProvider<EquipmentRequestSubject, EquipmentApprovalProvider>, IApprovalProvider<EquipmentRequestSubject>
{
    public override RequestModule RequestType => RequestModule.Equipment;

    public EquipmentApprovalProvider(
        IUnitOfWork uow,
        IEmailService email,
        IApprovalNotificationService notification,
        IEmployeeUserResolver userResolver,
        IApprovalRouteService routeService,
        IApprovalSelectionService selectionService,
        ILogger<EquipmentApprovalProvider> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(uow, email, notification, userResolver, routeService, selectionService, logger, options)
    {
    }

    public override async Task<EquipmentRequestSubject?> GetSubjectAsync(int requestId, CancellationToken ct)
    {
        var entity = await _uow.Repository<F03EquipmentRequest>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct);

        if (entity == null) return null;

        var employee = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.EmployeeCode == entity.EmployeeCode)
            .Select(x => new { x.EmployeeName, x.DeptCode, x.PositionCode })
            .FirstOrDefaultAsync(ct);

        return EquipmentRequestSubject.From(
            entity,
            employee?.EmployeeName,
            employee?.PositionCode);
    }

    public override async Task<List<EquipmentRequestSubject>> GetSubjectsAsync(
        List<int> requestIds,
        CancellationToken ct)
    {
        var rows = await _uow.Repository<F03EquipmentRequest>().Query()
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.Id) && x.IsActive == true)
            .ToListAsync(ct);

        var employeeCodes = rows
            .Select(x => x.EmployeeCode)
            .Distinct()
            .ToList();

        var employees = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => employeeCodes.Contains(x.EmployeeCode))
            .Select(x => new
            {
                x.EmployeeCode,
                x.EmployeeName,
                x.DeptCode,
                x.PositionCode
            })
            .ToDictionaryAsync(x => x.EmployeeCode, x => x, ct);

        return rows.Select(row =>
        {
            employees.TryGetValue(row.EmployeeCode, out var employee);
            return EquipmentRequestSubject.From(
                row,
                employee?.EmployeeName,
                employee?.PositionCode);
        }).ToList();
    }

    public override async Task ApplyOverallStatusAsync(
        int requestId,
        IReadOnlyList<ApprovalStepDto> allSteps,
        CancellationToken ct)
    {
        var entity = await _uow.Repository<F03EquipmentRequest>().Query()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (entity == null) return;

        var required = allSteps
            .Where(x => x.IsRequired)
            .ToList();

        entity.RequestStatus = required.Any(x => x.Decision == DecisionType.Rejected)
            ? ApprovalStatus.Rejected
            : required.Count > 0 && required.All(x => x.Decision == DecisionType.Approved)
                ? ApprovalStatus.Approved
                : required.Any(x => x.Decision == DecisionType.Approved)
                    ? ApprovalStatus.InProgress
                    : ApprovalStatus.Pending;

        if (entity.RequestKind == EquipmentRequestKind.Form && entity.FormSubmissionId.HasValue)
        {
            var submission = await _uow.Repository<F03PublicFormSubmission>().Query()
                .FirstOrDefaultAsync(x => x.Id == entity.FormSubmissionId.Value, ct);
            if (submission != null)
            {
                submission.Status = entity.RequestStatus switch
                {
                    ApprovalStatus.Approved => "Approved",
                    ApprovalStatus.Rejected => "Rejected",
                    ApprovalStatus.InProgress => "InProgress",
                    _ => "PendingApproval"
                };
            }
        }

        if (entity.RequestStatus == ApprovalStatus.Approved)
        {
            if (entity.RequestKind == EquipmentRequestKind.Registration && entity.AssetId == null)
            {
                if (entity.PurchaseDate.HasValue && entity.ExpectedDepreciationDate.HasValue)
                {
                    var asset = new F03EquipmentAsset
                    {
                        EquipmentCode = $"EQP-{Guid.NewGuid():N}"[..30],
                        EquipmentName = entity.EquipmentName,
                        Specification = entity.Specification,
                        SerialNumber = entity.SerialNumber,
                        AssetCode = entity.AssetCode,
                        PurchasePrice = entity.PurchasePrice,
                        PurchaseDate = entity.PurchaseDate.Value,
                        ExpectedDepreciationDate = entity.ExpectedDepreciationDate.Value,
                        DeptCode = entity.DeptCode!.Value,
                        Location = entity.Location,
                        QrToken = entity.QrToken,
                        IsQrActive = true,
                        Note = entity.Note,
                        EndpointAgentEligible = entity.EndpointAgentEligible,
                        EndpointOsFamily = entity.EndpointOsFamily,
                        CreatedBy = entity.OperatorUserId
                    };

                    await _uow.Repository<F03EquipmentAsset>().AddAsync(asset, ct);
                    await _uow.SaveChangesAsync(ct);
                    entity.AssetId = asset.Id;
                }
            }
            else if (entity.RequestKind == EquipmentRequestKind.AssetChange &&
                     entity.AssetId.HasValue &&
                     entity.AssetChangeAppliedAtUtc == null)
            {
                // Asset changes are applied only after every required approval succeeds.
                // Compare the captured business-state hash before writing to avoid overwriting
                // an asset that was edited by another workflow while this request was pending.
                var asset = await _uow.Repository<F03EquipmentAsset>().Query()
                    .FirstOrDefaultAsync(x => x.Id == entity.AssetId.Value && x.IsActive == true, ct);

                if (asset == null)
                {
                    entity.RequestStatus = ApprovalStatus.Rejected;
                    entity.Note = AppendWorkflowNote(entity.Note, "Không thể áp dụng thay đổi: thiết bị không còn tồn tại hoặc đã ngừng hoạt động.");
                }
                else if (string.IsNullOrWhiteSpace(entity.AssetBeforeSnapshotJson) ||
                         string.IsNullOrWhiteSpace(entity.AssetAfterSnapshotJson) ||
                         string.IsNullOrWhiteSpace(entity.AssetBeforeHash))
                {
                    entity.RequestStatus = ApprovalStatus.Rejected;
                    entity.Note = AppendWorkflowNote(entity.Note, "Không thể áp dụng thay đổi: thiếu snapshot trước/sau hoặc hash đối chiếu.");
                }
                else
                {
                    var currentHash = ComputeAssetBusinessHash(asset);
                    if (!string.Equals(currentHash, entity.AssetBeforeHash, StringComparison.OrdinalIgnoreCase))
                    {
                        entity.RequestStatus = ApprovalStatus.Rejected;
                        entity.Note = AppendWorkflowNote(entity.Note, "Xung đột dữ liệu: thiết bị đã thay đổi sau khi gửi yêu cầu. Cần tạo yêu cầu mới từ dữ liệu hiện tại.");
                    }
                    else
                    {
                        using var after = JsonDocument.Parse(entity.AssetAfterSnapshotJson);
                        var root = after.RootElement;
                        asset.EquipmentCode = ReadString(root, "EquipmentCode") ?? asset.EquipmentCode;
                        asset.EquipmentName = ReadString(root, "EquipmentName") ?? asset.EquipmentName;
                        asset.AssetCode = ReadString(root, "AssetCode");
                        asset.SerialNumber = ReadString(root, "SerialNumber");
                        asset.Specification = ReadString(root, "Specification");
                        asset.PurchasePrice = ReadDecimal(root, "PurchasePrice") ?? asset.PurchasePrice;
                        asset.PurchaseDate = ReadDate(root, "PurchaseDate") ?? asset.PurchaseDate;
                        asset.ExpectedDepreciationDate = ReadDate(root, "ExpectedDepreciationDate") ?? asset.ExpectedDepreciationDate;
                        asset.Location = ReadString(root, "Location");
                        asset.Note = ReadString(root, "Note");
                        asset.CustomDataJson = ReadString(root, "CustomDataJson") ?? asset.CustomDataJson;
                        asset.ModifiedAt = DateTime.UtcNow;
                        asset.ModifiedBy = entity.OperatorUserId;
                        entity.AssetChangeAppliedAtUtc = DateTime.UtcNow;
                    }
                }
            }
            else if (entity.RequestKind == EquipmentRequestKind.Repair &&
                     entity.AssetId.HasValue &&
                     entity.RepairDate.HasValue)
            {
                var exists = await _uow.Repository<F03EquipmentRepairHistory>().Query()
                    .AnyAsync(x => x.RequestId == entity.Id, ct);

                if (!exists)
                {
                    await _uow.Repository<F03EquipmentRepairHistory>().AddAsync(
                        new F03EquipmentRepairHistory
                        {
                            AssetId = entity.AssetId.Value,
                            RequestId = entity.Id,
                            RepairDate = entity.RepairDate.Value,
                            OperatorUserId = entity.OperatorUserId,
                            RepairCost = entity.RepairCost,
                            RepairContent = entity.RepairContent ?? string.Empty,
                            RepairVendor = entity.RepairVendor,
                            RepairResult = entity.RepairResult,
                            Note = entity.Note,
                            IsApproved = true,
                            CreatedBy = entity.OperatorUserId
                        }, ct);
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
    }

    private static string ComputeAssetBusinessHash(F03EquipmentAsset asset)
    {
        // Hash only editable business fields; audit timestamps and identity metadata must
        // not create false conflicts.
        var canonical = JsonSerializer.Serialize(new
        {
            asset.EquipmentCode, asset.EquipmentName, asset.AssetCode, asset.SerialNumber,
            asset.Specification, asset.PurchasePrice, asset.PurchaseDate,
            asset.ExpectedDepreciationDate, asset.Location, asset.Note, asset.CustomDataJson
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() : null;

    private static decimal? ReadDecimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var result) ? result : null;

    private static DateTime? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        value.TryGetDateTime(out var result) ? result : null;

    private static string AppendWorkflowNote(string? note, string message) =>
        string.IsNullOrWhiteSpace(note) ? message : $"{note.Trim()} | {message}";

    public override async Task NotifyStepCompletedAsync(
        EquipmentRequestSubject subject,
        ApprovalStepDto completedStep,
        bool isFullyApproved,
        CancellationToken ct)
    {
        if (!isFullyApproved && completedStep.Decision != DecisionType.Rejected)
            return;

        var email = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.EmployeeCode == subject.EmployeeCode)
            .Select(x => x.EmailAddress)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(email))
        {
            await _email.QueueEmail(
                email,
                isFullyApproved ? "EQUIPMENT_APPROVED" : "EQUIPMENT_REJECTED",
                new
                {
                    subject.RequestId,
                    subject.RequestKind,
                    subject.FormCode,
                    subject.EquipmentName,
                    subject.AssetCode,
                    subject.RepairDate,
                    Status = isFullyApproved ? "Approved" : "Rejected"
                },
                ct);
        }

        await NotifyEmployeeInAppAsync(
            subject,
            isFullyApproved ? ApprovalStatus.Approved : ApprovalStatus.Rejected,
            ct);
    }

    public override async Task<PendingApprovalItemDto> ToPendingItemAsync(
        EquipmentRequestSubject subject,
        List<ApprovalStepDto> steps,
        bool canApprove,
        CancellationToken ct)
    {
        var deptName = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(x => x.DeptCode == subject.DeptCode)
            .Select(x => x.DeptName)
            .FirstOrDefaultAsync(ct);

        return new PendingApprovalItemDto
        {
            RequestId = subject.RequestId,
            Kind = subject.Module,
            EmployeeCode = subject.EmployeeCode,
            EmployeeName = subject.EmployeeName ?? string.Empty,
            DeptCode = subject.DeptCode ?? 0,
            DeptName = deptName ?? string.Empty,
            FromDate = subject.RepairDate ?? DateTime.Now,
            ToDate = subject.RepairDate ?? DateTime.Now,
            TotalUnits = 1,
            ApprovalSteps = steps,
            CanApprove = canApprove
        };
    }
}

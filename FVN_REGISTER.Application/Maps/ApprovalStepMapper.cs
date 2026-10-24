using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Maps;

public static class ApprovalStepMapper
{
    public static ApprovalStepDto ToDto(
        F03ApprovalStepSnapshot snapshot,
        F03ApprovalHistory? history)
    {
        var decision = history?.Decision ?? DecisionType.Pending;

        return new ApprovalStepDto
        {
            Level = snapshot.Level,
            RoleName = snapshot.RoleName,
            IsRequired = snapshot.IsRequired,
            Decision = decision,
            ApproverCode = snapshot.ApproverCode,
            ApproverName = history?.ApproverName ?? snapshot.ApproverName,
            ApproverEmail = snapshot.ApproverEmail,
            Comment = history?.Comment,
            ApproveTime = history?.ActionAt,
            IsOverriddenByAdmin = history?.IsOverriddenByAdmin ?? false,
            OverriddenByName = history?.OverriddenByName,
            OverriddenAt = history?.ActionAt
        };
    }

    public static List<ApprovalStepDto> MapToList(
        IEnumerable<F03ApprovalStepSnapshot> snapshots,
        IEnumerable<F03ApprovalHistory> histories)
    {
        // Một bước có thể có nhiều history (ví dụ: hệ thống Escalated rồi người duyệt mới Approved).
        // Lấy quyết định mới nhất để tránh ToDictionary lỗi khóa trùng và phản ánh đúng trạng thái hiện tại.
        var historyByStepId = histories
            .GroupBy(h => h.StepId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(h => h.ActionAt)
                      .ThenByDescending(h => h.Id)
                      .First());

        return snapshots
            .OrderBy(s => s.Level)
            .Select(s =>
            {
                historyByStepId.TryGetValue(s.Id, out var history);
                return ToDto(s, history);
            })
            .ToList();
    }

    /// <summary>Bước vẫn có thể được người duyệt xử lý: đang chờ hoặc đã được hệ thống chuyển cấp.</summary>
    public static bool IsOpen(DecisionType decision) =>
        decision == DecisionType.Pending || decision == DecisionType.Escalated;

    /// <summary>Bước trước được xem là đã nhường quyền xử lý cho cấp sau khi đã duyệt hoặc được chuyển cấp.</summary>
    public static bool IsDoneForNext(DecisionType decision) =>
        decision == DecisionType.Approved || decision == DecisionType.Escalated;

    public static ApprovalStatus ComputeOverallStatus(IEnumerable<ApprovalStepDto> steps)
    {
        var requiredSteps = steps.Where(s => s.IsRequired).ToList();

        if (requiredSteps.Count == 0)
            return ApprovalStatus.Pending;

        if (requiredSteps.Any(s => s.Decision == DecisionType.Rejected))
            return ApprovalStatus.Rejected;

        if (requiredSteps.Any(s => s.Decision == DecisionType.Returned))
            return ApprovalStatus.NeedsRevision;

        if (requiredSteps.All(s => s.Decision == DecisionType.Approved))
            return ApprovalStatus.Approved;

        if (requiredSteps.Any(s => s.Decision == DecisionType.Approved))
            return ApprovalStatus.InProgress;

        return ApprovalStatus.Pending;
    }
}
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Deterministic first-pass comparison. The language model may later explain these results,
/// but must never change the outcome or invent official facts.
/// </summary>
public sealed class HRReconciliationService(IAuthorizedHRFactsProvider factsProvider) : IHRReconciliationService
{
    private const decimal HoursTolerance = 0.01m;

    public async Task<HRReconciliationAnalysis> AnalyzeAsync(string tenantCode, string actorId,
        HRCaseAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64, nameof(tenantCode));
        actorId = Required(actorId, 128, nameof(actorId));
        if (string.IsNullOrWhiteSpace(request.UserStatement) || request.UserStatement.Length > 8000)
            throw new ArgumentException("User statement must contain 1 to 8000 characters.", nameof(request));
        if (request.EvidenceText?.Length > 100_000)
            throw new ArgumentException("Evidence text exceeds the permitted length.", nameof(request));

        var facts = await factsProvider.GetFactsAsync(tenantCode, actorId, request.CaseType,
            request.EmployeeCode, request.WorkDate, cancellationToken);
        if (facts is null)
            throw new KeyNotFoundException("No authorized official HR facts were found for this employee and date.");

        if (facts.WorkDate != request.WorkDate)
            throw new InvalidOperationException("Official facts provider returned a different work date.");
        if (!string.IsNullOrWhiteSpace(request.EmployeeCode) &&
            !string.Equals(facts.EmployeeCode, request.EmployeeCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Official facts provider returned a different employee.");

        var comparisons = request.CaseType switch
        {
            HRCaseType.Overtime => CompareOvertime(facts),
            HRCaseType.Attendance => CompareAttendance(facts),
            HRCaseType.Leave => CompareLeave(facts),
            _ => throw new ArgumentOutOfRangeException(nameof(request.CaseType))
        };

        var outcome = comparisons.Count == 0 || comparisons.Any(x => x.Outcome == HRComparisonOutcome.InsufficientEvidence)
            ? HRComparisonOutcome.InsufficientEvidence
            : comparisons.Any(x => x.Outcome == HRComparisonOutcome.Discrepancy)
                ? HRComparisonOutcome.Discrepancy
                : comparisons.Any(x => x.Outcome == HRComparisonOutcome.NeedsHumanDecision)
                    ? HRComparisonOutcome.NeedsHumanDecision
                    : HRComparisonOutcome.Matched;

        var summary = outcome switch
        {
            HRComparisonOutcome.Matched => "Thông tin người dùng phản hồi hiện khớp với dữ liệu HR được truy xuất.",
            HRComparisonOutcome.Discrepancy => "Có điểm sai lệch giữa nội dung phản hồi và dữ liệu HR. HR cần kiểm tra các mục được liệt kê.",
            HRComparisonOutcome.InsufficientEvidence => "Chưa đủ dữ liệu để kết luận. Cần bổ sung bằng chứng hoặc kiểm tra nguồn dữ liệu.",
            _ => "Có tình huống cần HR xem xét theo quy định và ngữ cảnh nghiệp vụ."
        };
        var reply = $"Chào bạn, HR đã kiểm tra phản hồi ngày {request.WorkDate:dd/MM/yyyy}. {summary} HR sẽ xác minh các nội dung liên quan trước khi xác nhận kết quả cuối cùng.";
        var note = string.Join(Environment.NewLine, comparisons.Select(x => $"- {x.Field}: {x.Explanation} [{x.RuleCode}]"));
        if (string.IsNullOrWhiteSpace(note)) note = "Không có trường so sánh đủ dữ liệu.";
        return new HRReconciliationAnalysis(Guid.NewGuid(), request.CaseType, facts.EmployeeCode,
            facts.WorkDate, outcome, comparisons, reply, note,
            string.IsNullOrWhiteSpace(request.EvidenceReference) ? Array.Empty<string>() : new[] { request.EvidenceReference },
            facts.SourceSystem, facts.SourceRecordReference, facts.RetrievedAtUtc, true);
    }

    private static IReadOnlyList<HRFieldComparison> CompareOvertime(OfficialHRFacts f)
    {
        var result = new List<HRFieldComparison>();
        CompareHours(result, "PlannedHours", f.PlannedHours, "OT-PLAN-001",
            "Số giờ OT dự kiến được lấy từ yêu cầu OT chính thức.");
        CompareHours(result, "ActualHours", f.ActualHours, "OT-ACTUAL-001",
            "Số giờ OT thực tế được lấy từ dữ liệu xác nhận OT.");
        CompareHours(result, "AttendanceOvertimeHours", f.AttendanceOvertimeHours, "OT-ATTENDANCE-001",
            "Số giờ OT trên dữ liệu chấm công được dùng để đối chiếu, không thay thế xác nhận OT.");
        if (f.PlannedHours is null && f.ActualHours is null && f.AttendanceOvertimeHours is null)
            result.Add(new("OvertimeHours", null, null, "OT-MISSING-001", HRComparisonOutcome.InsufficientEvidence,
                "Không có số giờ OT chính thức để đối chiếu."));
        if (f.RequestStatus is null)
            result.Add(new("RequestStatus", null, null, "OT-STATUS-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu trạng thái yêu cầu OT."));
        else
            result.Add(new("RequestStatus", null, f.RequestStatus, "OT-STATUS-001",
                HRComparisonOutcome.NeedsHumanDecision, "Cần HR xem xét trạng thái yêu cầu cùng quy định phê duyệt."));
        return result;
    }

    private static IReadOnlyList<HRFieldComparison> CompareAttendance(OfficialHRFacts f)
    {
        var result = new List<HRFieldComparison>();
        if (f.ActualStart is null || f.ActualEnd is null)
            result.Add(new("AttendanceInterval", null, null, "ATT-MISSING-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu giờ vào hoặc giờ ra chính thức."));
        else
            result.Add(new("AttendanceInterval", null, $"{f.ActualStart:HH:mm} - {f.ActualEnd:HH:mm}",
                "ATT-SOURCE-001", HRComparisonOutcome.NeedsHumanDecision,
                "Đã lấy được khoảng thời gian chấm công; cần đối chiếu ca làm việc và quy định áp dụng."));
        if (f.ShiftCode is null)
            result.Add(new("ShiftCode", null, null, "ATT-SHIFT-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu mã ca làm việc."));
        return result;
    }

    private static IReadOnlyList<HRFieldComparison> CompareLeave(OfficialHRFacts f)
    {
        var result = new List<HRFieldComparison>();
        if (f.LeaveType is null || f.LeaveDays is null)
            result.Add(new("Leave", null, null, "LEAVE-MISSING-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu loại nghỉ hoặc số ngày nghỉ chính thức."));
        else
            result.Add(new("Leave", null, $"{f.LeaveType}: {f.LeaveDays:0.##} ngày",
                "LEAVE-SOURCE-001", HRComparisonOutcome.NeedsHumanDecision,
                "Đã lấy dữ liệu nghỉ phép; cần kiểm tra trạng thái phê duyệt và quy tắc tính ngày nghỉ."));
        return result;
    }

    private static void CompareHours(List<HRFieldComparison> result, string field, decimal? hours,
        string rule, string explanation)
    {
        result.Add(hours is null
            ? new(field, null, null, rule, HRComparisonOutcome.InsufficientEvidence, "Không có giá trị chính thức.")
            : new(field, null, hours.Value.ToString("0.##") + " giờ", rule,
                HRComparisonOutcome.NeedsHumanDecision, explanation));
    }

    private static string Required(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ArgumentException($"Value must contain 1 to {max} characters.", name);
        return value.Trim();
    }
}

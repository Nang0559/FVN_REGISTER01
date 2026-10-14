using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Deterministic first-pass comparison. The language model may later explain these results,
/// but must never change the outcome or invent official facts.
/// </summary>
public sealed class HRReconciliationService(IAuthorizedHRFactsProvider factsProvider, AiPlatformDbContext db) : IHRReconciliationService
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
            HRCaseType.Overtime => CompareOvertime(facts, request),
            HRCaseType.Attendance => CompareAttendance(facts, request),
            HRCaseType.Leave => CompareLeave(facts, request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.CaseType))
        };

        var outcome = comparisons.Any(x => x.Outcome == HRComparisonOutcome.Discrepancy)
            ? HRComparisonOutcome.Discrepancy
            : comparisons.Count == 0 || comparisons.Any(x => x.Outcome == HRComparisonOutcome.InsufficientEvidence)
                ? HRComparisonOutcome.InsufficientEvidence
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
        var caseId = Guid.NewGuid();
        var analysis = new HRReconciliationAnalysis(caseId, request.CaseType, facts.EmployeeCode,
            facts.WorkDate, outcome, comparisons, reply, note,
            string.IsNullOrWhiteSpace(request.EvidenceReference) ? Array.Empty<string>() : new[] { request.EvidenceReference },
            facts.SourceSystem, facts.SourceRecordReference, facts.RetrievedAtUtc, true);
        db.HRCases.Add(new AiHRCaseRecord
        {
            Id = caseId, TenantCode = tenantCode, EmployeeCode = facts.EmployeeCode,
            CaseType = request.CaseType.ToString(), WorkDate = facts.WorkDate, Outcome = outcome.ToString(),
            UserStatement = request.UserStatement, EvidenceReference = request.EvidenceReference,
            AnalysisJson = JsonSerializer.Serialize(analysis), SuggestedReply = reply,
            InternalHRNote = note, OfficialSource = facts.SourceSystem,
            OfficialRecordReference = facts.SourceRecordReference, FactsRetrievedAtUtc = facts.RetrievedAtUtc,
            Status = "PendingHRReview", CreatedBy = actorId, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return analysis;
    }

    private static IReadOnlyList<HRFieldComparison> CompareOvertime(OfficialHRFacts f, HRCaseAnalysisRequest request)
    {
        var result = new List<HRFieldComparison>();
        CompareHours(result, "ReportedHoursVsPlanned", request.ReportedHours, f.PlannedHours, "OT-PLAN-001", "So sánh số giờ người dùng phản hồi với số giờ đăng ký chính thức.");
        CompareHours(result, "ReportedHoursVsActual", request.ReportedHours, f.ActualHours, "OT-ACTUAL-001", "So sánh số giờ người dùng phản hồi với số giờ OT thực tế đã xác nhận.");
        CompareHours(result, "ActualVsAttendance", f.ActualHours, f.AttendanceOvertimeHours, "OT-ATTENDANCE-001", "So sánh OT thực tế đã xác nhận với OT trên dữ liệu chấm công.");
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

    private static IReadOnlyList<HRFieldComparison> CompareAttendance(OfficialHRFacts f, HRCaseAnalysisRequest request)
    {
        var result = new List<HRFieldComparison>();
        if (f.ActualStart is null || f.ActualEnd is null)
            result.Add(new("AttendanceInterval", null, null, "ATT-MISSING-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu giờ vào hoặc giờ ra chính thức."));
        else
        {
            var hasReportedInterval = request.ReportedStart.HasValue && request.ReportedEnd.HasValue;
            var differs = IntervalsDiffer(request.ReportedStart, request.ReportedEnd, f.ActualStart, f.ActualEnd);
            result.Add(new("AttendanceInterval", FormatInterval(request.ReportedStart, request.ReportedEnd), $"{f.ActualStart:HH:mm} - {f.ActualEnd:HH:mm}",
                "ATT-SOURCE-001", !hasReportedInterval ? HRComparisonOutcome.InsufficientEvidence : differs ? HRComparisonOutcome.Discrepancy : HRComparisonOutcome.Matched,
                !hasReportedInterval ? "Thiếu khoảng thời gian người dùng cung cấp để đối chiếu." : differs ? "Khoảng thời gian người dùng cung cấp khác dữ liệu chấm công." : "Khoảng thời gian khớp dữ liệu chấm công."));
        }
        if (f.ShiftCode is null)
            result.Add(new("ShiftCode", null, null, "ATT-SHIFT-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu mã ca làm việc."));
        return result;
    }

    private static IReadOnlyList<HRFieldComparison> CompareLeave(OfficialHRFacts f, HRCaseAnalysisRequest request)
    {
        var result = new List<HRFieldComparison>();
        if (f.LeaveType is null || f.LeaveDays is null)
            result.Add(new("Leave", null, null, "LEAVE-MISSING-001", HRComparisonOutcome.InsufficientEvidence,
                "Thiếu loại nghỉ hoặc số ngày nghỉ chính thức."));
        else
        {
            var differs = (request.ReportedLeaveDays.HasValue && Math.Abs(request.ReportedLeaveDays.Value - f.LeaveDays.Value) > HoursTolerance) || (!string.IsNullOrWhiteSpace(request.ReportedLeaveType) && !string.Equals(request.ReportedLeaveType, f.LeaveType, StringComparison.OrdinalIgnoreCase));
            result.Add(new("Leave", request.ReportedLeaveType is null && request.ReportedLeaveDays is null ? null : $"{request.ReportedLeaveType ?? "?"}: {request.ReportedLeaveDays?.ToString("0.##") ?? "?"} ngày", $"{f.LeaveType}: {f.LeaveDays:0.##} ngày", "LEAVE-SOURCE-001", differs ? HRComparisonOutcome.Discrepancy : (request.ReportedLeaveDays.HasValue || !string.IsNullOrWhiteSpace(request.ReportedLeaveType) ? HRComparisonOutcome.Matched : HRComparisonOutcome.NeedsHumanDecision), differs ? "Thông tin nghỉ người dùng cung cấp khác dữ liệu nghỉ phép chính thức." : "Dữ liệu nghỉ phép đã được truy xuất; HR cần kiểm tra trạng thái và quy tắc áp dụng."));
        }
        return result;
    }

    private static void CompareHours(List<HRFieldComparison> result, string field, decimal? reported,
        decimal? official, string rule, string explanation)
    {
        var outcome = reported is null || official is null
            ? HRComparisonOutcome.InsufficientEvidence
            : Math.Abs(reported.Value - official.Value) > HoursTolerance
                ? HRComparisonOutcome.Discrepancy : HRComparisonOutcome.Matched;
        result.Add(new(field, reported.HasValue ? reported.Value.ToString("0.##") + " giờ" : null,
            official.HasValue ? official.Value.ToString("0.##") + " giờ" : null, rule, outcome,
            reported is null || official is null ? "Thiếu một trong hai giá trị để so sánh."
                : outcome == HRComparisonOutcome.Discrepancy
                    ? $"Chênh lệch {Math.Abs(reported.Value - official.Value):0.##} giờ. {explanation}"
                    : explanation));
    }

    private static bool IntervalsDiffer(DateTimeOffset? rs, DateTimeOffset? re, DateTimeOffset? os, DateTimeOffset? oe)
        => !rs.HasValue || !re.HasValue || !os.HasValue || !oe.HasValue
            || Math.Abs((rs.Value - os.Value).TotalMinutes) > 1
            || Math.Abs((re.Value - oe.Value).TotalMinutes) > 1;

    private static string? FormatInterval(DateTimeOffset? start, DateTimeOffset? end)
        => start.HasValue && end.HasValue ? $"{start:HH:mm} - {end:HH:mm}" : null;

    private static string Required(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ArgumentException($"Value must contain 1 to {max} characters.", name);
        return value.Trim();
    }
}

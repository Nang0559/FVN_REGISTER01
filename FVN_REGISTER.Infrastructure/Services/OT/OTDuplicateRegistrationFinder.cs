using FVN_REGISTER.Core.Entities;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.OT;

/// <summary>An employee who already has a live OT request on a given day.</summary>
public sealed record OTDuplicateRegistration(
    string EmployeeCode,
    string? EmployeeName,
    int OTRequestId,
    string? OTCode,
    ApprovalStatus Status,
    string CreatedByCode,
    string? CreatedByName);

/// <summary>
/// Business rule: one employee can be registered for OT only ONCE per day, no matter who creates
/// the request (the employee himself or a colleague/leader of the same department).
/// A request blocks the day unless it was Rejected or Cancelled (Draft, Pending, InProgress,
/// Escalated, NeedsRevision and Approved all count).
/// </summary>
public static class OTDuplicateRegistrationFinder
{
    public static async Task<IReadOnlyList<OTDuplicateRegistration>> FindAsync(
        IUnitOfWork uow,
        DateTime otDate,
        IEnumerable<string> employeeCodes,
        int? excludeOTRequestId,
        CancellationToken ct = default)
    {
        var codes = employeeCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count == 0)
            return Array.Empty<OTDuplicateRegistration>();

        var from = otDate.Date;
        var to = from.AddDays(1);

        var rows = await uow.Repository<F03OTEmployee>().Query()
            .AsNoTracking()
            .Where(e =>
                codes.Contains(e.EmployeeCode) &&
                e.IsActive == true &&
                e.OTRequest.IsActive == true &&
                e.OTRequest.OTDate >= from &&
                e.OTRequest.OTDate < to &&
                e.OTRequest.RequestStatus != ApprovalStatus.Rejected &&
                e.OTRequest.RequestStatus != ApprovalStatus.Cancelled &&
                (excludeOTRequestId == null || e.OTRequestId != excludeOTRequestId.Value))
            .Select(e => new
            {
                e.EmployeeCode,
                e.OTRequestId,
                e.OTRequest.OTCode,
                e.OTRequest.RequestStatus,
                CreatedByCode = e.OTRequest.EmployeeCode
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Array.Empty<OTDuplicateRegistration>();

        var nameCodes = rows.Select(x => x.EmployeeCode)
            .Concat(rows.Select(x => x.CreatedByCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var names = await uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(e => nameCodes.Contains(e.EmployeeCode ?? string.Empty))
            .Select(e => new { e.EmployeeCode, e.EmployeeName })
            .ToListAsync(ct);
        var nameByCode = names
            .Where(x => x.EmployeeCode != null)
            .GroupBy(x => x.EmployeeCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName, StringComparer.OrdinalIgnoreCase);

        string? NameOf(string? code) =>
            code != null && nameByCode.TryGetValue(code, out var n) ? n : null;

        // One entry per employee (the earliest request wins if several slipped through before this rule).
        return rows
            .GroupBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.OTRequestId).First())
            .Select(x => new OTDuplicateRegistration(
                x.EmployeeCode, NameOf(x.EmployeeCode), x.OTRequestId, x.OTCode,
                x.RequestStatus, x.CreatedByCode, NameOf(x.CreatedByCode)))
            .OrderBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string StatusLabel(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Draft => "Nháp",
        ApprovalStatus.Pending => "Chờ duyệt",
        ApprovalStatus.InProgress => "Đang duyệt",
        ApprovalStatus.Escalated => "Đã chuyển cấp",
        ApprovalStatus.Approved => "Đã duyệt",
        ApprovalStatus.NeedsRevision => "Cần chỉnh sửa",
        _ => status.ToString()
    };

    /// <summary>
    /// "Nhân viên A - Tên đã có đơn OT ngày dd/MM/yyyy (OT-001, Chờ duyệt) do chính nhân viên tạo / do B - Tên tạo."
    /// </summary>
    public static string Describe(OTDuplicateRegistration d, DateTime otDate)
    {
        var who = string.IsNullOrWhiteSpace(d.EmployeeName) ? d.EmployeeCode : $"{d.EmployeeCode} - {d.EmployeeName}";
        var code = string.IsNullOrWhiteSpace(d.OTCode) ? $"#{d.OTRequestId}" : d.OTCode;
        var creator = string.Equals(d.CreatedByCode, d.EmployeeCode, StringComparison.OrdinalIgnoreCase)
            ? "do chính nhân viên tạo"
            : $"do {(string.IsNullOrWhiteSpace(d.CreatedByName) ? d.CreatedByCode : $"{d.CreatedByCode} - {d.CreatedByName}")} đăng ký";

        return $"Nhân viên {who} đã có đơn OT ngày {otDate:dd/MM/yyyy} ({code}, {StatusLabel(d.Status)}) {creator}. " +
               "Mỗi nhân viên chỉ được đăng ký OT một lần trong ngày; hãy mở đơn hiện có để chỉnh sửa hoặc hủy trước khi đăng ký lại.";
    }

    public static List<string> DescribeAll(IEnumerable<OTDuplicateRegistration> duplicates, DateTime otDate) =>
        duplicates.Select(d => Describe(d, otDate)).ToList();
}

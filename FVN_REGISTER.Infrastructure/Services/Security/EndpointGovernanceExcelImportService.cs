using ClosedXML.Excel;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Infrastructure.Services.Security;

/// <summary>
/// Excel adapter only. It converts a workbook into the canonical Endpoint Governance
/// contract; persistence and approval remain in EndpointGovernanceService/Common Approval.
/// </summary>
public sealed class EndpointGovernanceExcelImportService
{
    private static readonly string[] NameHeaders = ["Name", "SoftwareName", "ServiceName", "Tên", "Tên phần mềm", "Tên dịch vụ", "NormalizedName"];
    private static readonly string[] PublisherHeaders = ["Publisher", "Nhà cung cấp", "Manufacturer"];
    private static readonly string[] VersionHeaders = ["VersionConstraint", "Version", "AllowedVersion", "Phiên bản", "Điều kiện phiên bản"];
    private static readonly string[] AllowedHeaders = ["IsAllowed", "Allowed", "Allow", "Được phép", "Cho phép"];
    private static readonly string[] RemarkHeaders = ["Remark", "Note", "Ghi chú", "Description", "Mô tả"];

    public async Task<EndpointGovernanceExcelPreviewDto> PreviewAsync(Stream stream, string fileName, EndpointGovernanceItemType itemType, EndpointTargetType targetType, CancellationToken ct = default)
    {
        if (stream == null || !stream.CanRead) throw new ArgumentException("Excel stream không hợp lệ.");
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        memory.Position = 0;

        using var workbook = new XLWorkbook(memory);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws == null) return new(fileName, itemType, targetType, Path.GetFileNameWithoutExtension(fileName), 0, [], ["File Excel không có worksheet."]);

        var used = ws.RangeUsed();
        if (used == null) return new(fileName, itemType, targetType, Path.GetFileNameWithoutExtension(fileName), 0, [], ["Worksheet không có dữ liệu."]);

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = firstCol; c <= lastCol; c++)
        {
            var value = NormalizeHeader(ws.Cell(firstRow, c).GetString());
            if (!string.IsNullOrWhiteSpace(value) && !headers.ContainsKey(value)) headers[value] = c;
        }

        var nameCol = Find(headers, NameHeaders);
        var publisherCol = Find(headers, PublisherHeaders);
        var versionCol = Find(headers, VersionHeaders);
        var allowedCol = Find(headers, AllowedHeaders);
        var remarkCol = Find(headers, RemarkHeaders);
        if (!nameCol.HasValue)
            return new(fileName, itemType, targetType, Path.GetFileNameWithoutExtension(fileName), 0, [], ["Không tìm thấy cột Name/Tên. Có thể dùng cột: Name, Publisher, VersionConstraint, IsAllowed, Remark."]);

        var rows = new List<EndpointGovernanceExcelRowDto>();
        var errors = new List<string>();
        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            var name = ws.Cell(r, nameCol.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                if (Enumerable.Range(firstCol, lastCol - firstCol + 1).Any(c => !string.IsNullOrWhiteSpace(ws.Cell(r, c).GetString())))
                    rows.Add(new(r, string.Empty, null, null, false, null, "Tên là bắt buộc."));
                continue;
            }

            var publisher = publisherCol.HasValue ? NullIfEmpty(ws.Cell(r, publisherCol.Value).GetString()) : null;
            var version = versionCol.HasValue ? NullIfEmpty(ws.Cell(r, versionCol.Value).GetString()) : null;
            var remark = remarkCol.HasValue ? NullIfEmpty(ws.Cell(r, remarkCol.Value).GetString()) : null;
            var allowed = true;
            string? rowError = null;
            if (allowedCol.HasValue)
            {
                var raw = ws.Cell(r, allowedCol.Value).GetString().Trim();
                if (!TryParseBoolean(raw, out allowed)) rowError = $"IsAllowed không hợp lệ: '{raw}'. Dùng true/false, 1/0, Yes/No hoặc Có/Không.";
            }
            rows.Add(new(r, name, publisher, version, allowed, remark, rowError));
            if (rowError != null) errors.Add($"Dòng {r}: {rowError}");
        }

        var duplicateNames = rows.Where(x => string.IsNullOrWhiteSpace(x.Error)).GroupBy(x => NormalizeName(x.Name)).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var duplicate in duplicateNames) errors.Add($"Trùng tên sau chuẩn hóa: {duplicate}.");
        var suggested = Path.GetFileNameWithoutExtension(fileName).Trim();
        return new(fileName, itemType, targetType, string.IsNullOrWhiteSpace(suggested) ? $"Endpoint {itemType}" : suggested, rows.Count, rows, errors.Distinct().ToList());
    }

    public static EndpointGovernancePolicyUpsertRequest ToUpsert(EndpointGovernanceExcelPreviewDto preview, EndpointGovernanceExcelImportRequest request)
    {
        if (preview.Errors.Count > 0) throw new InvalidOperationException("Excel còn lỗi, không thể tạo policy version.");
        var items = preview.Rows.Select(x => new EndpointGovernancePolicyItemDto(
            0,
            request.ItemType,
            NormalizeName(x.Name),
            x.Name.Trim(),
            x.Publisher,
            x.VersionConstraint,
            x.IsAllowed,
            x.Remark)).ToList();
        return new(request.PolicyCode.Trim(), request.PolicyName.Trim(), request.ItemType, request.TargetType, request.Remark?.Trim(), items);
    }

    private static int? Find(Dictionary<string, int> headers, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizeHeader(candidate);
            if (headers.TryGetValue(normalized, out var col)) return col;
        }
        return null;
    }

    private static string NormalizeHeader(string value) => value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseBoolean(string value, out bool result)
    {
        if (bool.TryParse(value, out result)) return true;
        if (value is "1" or "Y" or "YES" or "TRUE" or "Có" or "CO") { result = true; return true; }
        if (value is "0" or "N" or "NO" or "FALSE" or "Không" or "KHONG") { result = false; return true; }
        result = false;
        return false;
    }
}

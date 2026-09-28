using System.Globalization;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Security;

/// <summary>Evaluates trusted Endpoint Agent inventory against the currently published catalog and endpoint security baseline.</summary>
public sealed class EndpointComplianceService : IEndpointComplianceService
{
    private const int MaxAntivirusDefinitionAgeDays = 7;
    private readonly IUnitOfWork _uow;
    public EndpointComplianceService(IUnitOfWork uow) => _uow = uow;

    public async Task<int> EvaluateAsync(long endpointDeviceId, CancellationToken cancellationToken = default)
    {
        var device = await _uow.Repository<F03EndpointDevice>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == endpointDeviceId, cancellationToken);
        if (device == null) return 0;
        var targetType = IsServer(device.OsName) ? EndpointTargetType.Server : EndpointTargetType.Workstation;
        var now = DateTime.UtcNow;

        var policies = await _uow.Repository<F03EndpointGovernancePolicy>().Query().AsNoTracking()
            .Where(x => x.IsActive == true && x.IsPublished && x.EffectiveFromUtc <= now && (x.EffectiveToUtc == null || x.EffectiveToUtc >= now) && (x.TargetType == targetType || x.TargetType == EndpointTargetType.Both))
            .OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        var policyIds = policies.Select(x => x.Id).ToList();
        var policyItems = policyIds.Count == 0 ? new List<F03EndpointGovernancePolicyItem>() : await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => policyIds.Contains(x.PolicyId) && x.IsActive == true).ToListAsync(cancellationToken);

        var softwarePolicy = policies.FirstOrDefault(x => x.ItemType == EndpointGovernanceItemType.Software);
        var servicePolicy = policies.FirstOrDefault(x => x.ItemType == EndpointGovernanceItemType.WindowsService);
        var softwareItems = softwarePolicy == null ? [] : policyItems.Where(x => x.PolicyId == softwarePolicy.Id).ToList();
        var serviceItems = servicePolicy == null ? [] : policyItems.Where(x => x.PolicyId == servicePolicy.Id).ToList();
        var software = await _uow.Repository<F03EndpointSoftwareInventory>().Query().AsNoTracking().Where(x => x.EndpointDeviceId == endpointDeviceId).ToListAsync(cancellationToken);
        var services = await _uow.Repository<F03EndpointServiceInventory>().Query().AsNoTracking().Where(x => x.EndpointDeviceId == endpointDeviceId).ToListAsync(cancellationToken);
        var antivirus = await _uow.Repository<F03EndpointAntivirusInventory>().Query().AsNoTracking().Where(x => x.EndpointDeviceId == endpointDeviceId).ToListAsync(cancellationToken);
        var openFindings = await _uow.Repository<F03EndpointComplianceFinding>().Query().Where(x => x.EndpointDeviceId == endpointDeviceId && x.ResolvedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var old in openFindings) old.ResolvedAtUtc = now;

        var count = 0;
        foreach (var item in software) { await _uow.Repository<F03EndpointComplianceFinding>().AddAsync(EvaluateSoftware(device, softwarePolicy, softwareItems, item, now), cancellationToken); count++; }
        foreach (var item in services) { await _uow.Repository<F03EndpointComplianceFinding>().AddAsync(EvaluateService(device, servicePolicy, serviceItems, item, now), cancellationToken); count++; }
        if (antivirus.Count == 0)
        {
            await _uow.Repository<F03EndpointComplianceFinding>().AddAsync(CreateFinding(device, null, null, EndpointGovernanceItemType.Antivirus, "Antivirus", null, EndpointComplianceResult.NonCompliant, "ANTIVIRUS_NOT_DETECTED", "Endpoint Agent không phát hiện antivirus/security product hoạt động trên máy.", now), cancellationToken);
            count++;
        }
        else
        {
            foreach (var item in antivirus) { await _uow.Repository<F03EndpointComplianceFinding>().AddAsync(EvaluateAntivirus(device, item, now), cancellationToken); count++; }
        }
        await _uow.SaveChangesAsync(cancellationToken);
        return count;
    }

    private static F03EndpointComplianceFinding EvaluateAntivirus(F03EndpointDevice device, F03EndpointAntivirusInventory item, DateTime at)
    {
        if (item.AntivirusEnabled == false || item.RealTimeProtectionEnabled == false)
            return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.ProductVersion, EndpointComplianceResult.NonCompliant, "ANTIVIRUS_PROTECTION_DISABLED", "Antivirus hoặc Real-time Protection đang tắt.", at);
        if (item.AntivirusEnabled is null || item.RealTimeProtectionEnabled is null)
            return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.ProductVersion, EndpointComplianceResult.Unknown, "ANTIVIRUS_STATUS_UNKNOWN", "Không xác định được đầy đủ trạng thái bảo vệ antivirus.", at);
        if (!string.Equals(item.ProtectionStatus, "Protected", StringComparison.OrdinalIgnoreCase))
            return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.ProductVersion, EndpointComplianceResult.NonCompliant, "ANTIVIRUS_NOT_PROTECTED", $"Trạng thái bảo vệ hiện tại: {item.ProtectionStatus ?? "Unknown"}.", at);
        if (!item.DefinitionUpdatedAtUtc.HasValue)
            return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.DefinitionVersion, EndpointComplianceResult.Unknown, "ANTIVIRUS_DEFINITION_UNKNOWN", "Không có thời điểm cập nhật database/signature antivirus.", at);
        var age = at - item.DefinitionUpdatedAtUtc.Value;
        if (age > TimeSpan.FromDays(MaxAntivirusDefinitionAgeDays))
            return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.DefinitionVersion, EndpointComplianceResult.NonCompliant, "ANTIVIRUS_DEFINITION_STALE", $"Database/signature antivirus đã quá {MaxAntivirusDefinitionAgeDays} ngày chưa cập nhật.", at);
        return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Antivirus, item.ProductName, item.DefinitionVersion, EndpointComplianceResult.Compliant, "ANTIVIRUS_COMPLIANT", "Antivirus đang hoạt động và database/signature còn trong thời hạn kiểm tra.", at);
    }

    private static F03EndpointComplianceFinding EvaluateSoftware(F03EndpointDevice device, F03EndpointGovernancePolicy? policy, IReadOnlyList<F03EndpointGovernancePolicyItem> policyItems, F03EndpointSoftwareInventory item, DateTime at)
    {
        var name = item.DisplayName ?? item.NormalizedName;
        if (policy == null) return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.Unknown, "NO_ACTIVE_POLICY", "Chưa có Software Catalog đang được publish cho loại endpoint này.", at);
        var match = policyItems.FirstOrDefault(x => string.Equals(x.NormalizedName, item.NormalizedName, StringComparison.OrdinalIgnoreCase));
        if (match == null) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.NonCompliant, "NOT_IN_CATALOG", "Phần mềm không có trong danh sách phần mềm được phép.", at);
        if (!match.IsAllowed) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.NonCompliant, "EXPLICITLY_DENIED", "Phần mềm có trong catalog nhưng item đang bị đánh dấu không được phép.", at, match.Id);
        if (!PublisherMatches(match.Publisher, item.Publisher)) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.NonCompliant, "PUBLISHER_MISMATCH", "Publisher thực tế không khớp publisher được phê duyệt.", at, match.Id);
        if (!VersionMatches(item.Version, match.VersionConstraint)) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.NonCompliant, "VERSION_NOT_ALLOWED", $"Phiên bản thực tế không thỏa điều kiện '{match.VersionConstraint}'.", at, match.Id);
        return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.Software, name, item.Version, EndpointComplianceResult.Compliant, "COMPLIANT", "Phần mềm phù hợp catalog đã publish.", at, match.Id);
    }

    private static F03EndpointComplianceFinding EvaluateService(F03EndpointDevice device, F03EndpointGovernancePolicy? policy, IReadOnlyList<F03EndpointGovernancePolicyItem> policyItems, F03EndpointServiceInventory item, DateTime at)
    {
        var name = item.DisplayName ?? item.ServiceName;
        if (policy == null) return CreateFinding(device, null, item.Id, EndpointGovernanceItemType.WindowsService, name, null, EndpointComplianceResult.Unknown, "NO_ACTIVE_POLICY", "Chưa có Windows Service Catalog đang được publish cho loại endpoint này.", at);
        var match = policyItems.FirstOrDefault(x => string.Equals(x.NormalizedName, Normalize(item.ServiceName), StringComparison.OrdinalIgnoreCase));
        if (match == null) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.WindowsService, name, null, EndpointComplianceResult.NonCompliant, "NOT_IN_CATALOG", "Windows Service không có trong danh sách dịch vụ được phép.", at);
        if (!match.IsAllowed) return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.WindowsService, name, null, EndpointComplianceResult.NonCompliant, "EXPLICITLY_DENIED", "Windows Service có trong catalog nhưng item đang bị đánh dấu không được phép.", at, match.Id);
        return CreateFinding(device, policy, item.Id, EndpointGovernanceItemType.WindowsService, name, null, EndpointComplianceResult.Compliant, "COMPLIANT", "Windows Service phù hợp catalog đã publish.", at, match.Id);
    }

    private static F03EndpointComplianceFinding CreateFinding(F03EndpointDevice device, F03EndpointGovernancePolicy? policy, long? inventoryItemId, EndpointGovernanceItemType itemType, string observedName, string? observedVersion, EndpointComplianceResult result, string code, string message, DateTime at, int? policyItemId = null)
        => new()
        {
            EndpointDeviceId = device.Id, PolicyId = policy?.Id, PolicyVersion = policy?.Version ?? 0, PolicyItemId = policyItemId,
            ItemType = itemType, InventoryItemId = inventoryItemId, Result = result, ObservedName = observedName, ObservedVersion = observedVersion,
            FindingCode = code, FindingMessage = message, EvaluatedAtUtc = at,
            ResolvedAtUtc = result == EndpointComplianceResult.Compliant ? at : null,
            CreatedAt = at, CreatedBy = 0, LastModifiedSource = "EndpointComplianceService"
        };

    private static bool IsServer(string? osName) => !string.IsNullOrWhiteSpace(osName) && osName.Contains("server", StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static bool PublisherMatches(string? expected, string? actual) => string.IsNullOrWhiteSpace(expected) || string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);

    private static bool VersionMatches(string? actual, string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint) || constraint.Trim() == "*") return true;
        if (string.IsNullOrWhiteSpace(actual)) return false;
        foreach (var part in constraint.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) if (!CompareOne(actual.Trim(), part)) return false;
        return true;
    }

    private static bool CompareOne(string actualText, string constraint)
    {
        var op = "="; var value = constraint.Trim();
        foreach (var candidate in new[] { ">=", "<=", ">", "<", "=" }) { if (!value.StartsWith(candidate, StringComparison.Ordinal)) continue; op = candidate; value = value[candidate.Length..].Trim(); break; }
        if (value == "*") return true;
        if (!TryVersion(actualText, out var actual) || !TryVersion(value, out var expected)) return string.Equals(actualText, value, StringComparison.OrdinalIgnoreCase);
        var cmp = actual.CompareTo(expected);
        return op switch { ">" => cmp > 0, ">=" => cmp >= 0, "<" => cmp < 0, "<=" => cmp <= 0, _ => cmp == 0 };
    }

    private static bool TryVersion(string value, out Version version)
    {
        if (Version.TryParse(value.Trim(), out version!)) return true;
        var parts = value.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(x => !int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))) { version = new Version(0, 0); return false; }
        version = new Version(string.Join('.', parts));
        return true;
    }
}

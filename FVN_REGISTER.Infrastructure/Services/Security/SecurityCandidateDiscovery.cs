using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.Core.Attributes;
using FVN_REGISTER.Core.Entities.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Security;

/// <summary>
/// Discovers security-function candidates from compiled API endpoints and UI components.
/// API endpoint mapping is explicit: SecurityFunctionDefinitionAttribute is the only
/// source that can associate an endpoint with a FunctionKey. Method-name prefixes are
/// never used to assign a FunctionCode. An [Authorize] endpoint without an explicit
/// definition is recorded only as an unmapped review candidate (FunctionCode = 0).
/// </summary>
public sealed class SecurityCandidateDiscovery
{
    private static readonly string[] CandidateActionWords =
    [
        "Create", "Add", "Update", "Edit", "Delete", "Remove", "Approve", "Reject", "Cancel",
        "Submit", "Assign", "Handover", "Transfer", "Return", "Repair", "Import", "Export",
        "Upload", "Download", "Print", "Sync", "Calculate", "Recalculate", "Lock", "Unlock",
        "Reset", "Generate", "Send", "Publish", "Unpublish", "Archive", "Restore", "Execute"
    ];

    private readonly FVNWEBAPPContext _db;
    private readonly IEnumerable<EndpointDataSource> _endpointSources;

    public SecurityCandidateDiscovery(FVNWEBAPPContext db, IEnumerable<EndpointDataSource> endpointSources)
    {
        _db = db;
        _endpointSources = endpointSources;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var registry = await _db.SecurityFunctionRegistry
            .Where(x => x.SourceType == "ApiEndpointDefinition"
                     || x.SourceType == "ApiEndpointUnmapped"
                     || x.SourceType == "UiActionCandidate")
            .ToDictionaryAsync(x => x.FunctionKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var functions = await _db.Functions
            .AsNoTracking()
            .Where(x => x.IsActive == true && !string.IsNullOrWhiteSpace(x.FunctionKey))
            .ToDictionaryAsync(x => x.FunctionKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var seenApi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenUi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.Now;

        foreach (var endpoint in _endpointSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>())
        {
            var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (action is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                continue;

            var definition = action.MethodInfo.GetCustomAttribute<SecurityFunctionDefinitionAttribute>()
                             ?? action.ControllerTypeInfo.GetCustomAttribute<SecurityFunctionDefinitionAttribute>();

            var requiresAuthorization = endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null
                                         || action.MethodInfo.GetCustomAttribute<AuthorizeAttribute>() is not null
                                         || action.ControllerTypeInfo.GetCustomAttribute<AuthorizeAttribute>() is not null;

            var controller = TrimControllerSuffix(action.ControllerTypeInfo.Name);
            var sourceName = $"{action.ControllerTypeInfo.FullName}.{action.MethodInfo.Name}";
            var evidence = endpoint.RoutePattern.RawText ?? string.Empty;

            if (definition is null || string.IsNullOrWhiteSpace(definition.FunctionKey))
            {
                if (!requiresAuthorization)
                    continue;

                var unmappedKey = $"Unmapped.Api.{sourceName}";
                seenApi.Add(unmappedKey);
                UpsertCandidate(
                    registry,
                    unmappedKey,
                    $"API endpoint yêu cầu authorization nhưng chưa khai báo SecurityFunctionDefinition: {sourceName}",
                    unmappedKey,
                    0,
                    controller,
                    action.MethodInfo.Name,
                    "ApiEndpointUnmapped",
                    action.ControllerTypeInfo.Assembly.GetName().Name,
                    sourceName,
                    evidence,
                    "Review",
                    now);
                continue;
            }

            var functionKey = definition.FunctionKey.Trim();
            var functionCode = functions.TryGetValue(functionKey, out var function) ? function.FunctionCode : 0;
            var moduleCode = definition.ModuleCode ?? function?.ModuleCode ?? controller;
            var actionCode = definition.ActionCode ?? function?.ActionCode ?? action.MethodInfo.Name;
            var scopeCode = definition.ScopeCode ?? function?.ScopeCode ?? "Review";
            var candidateKey = $"Endpoint.{functionKey}";
            seenApi.Add(candidateKey);

            UpsertCandidate(
                registry,
                candidateKey,
                definition.DisplayName ?? function?.FunctionName ?? functionKey,
                functionKey,
                functionCode,
                moduleCode,
                actionCode,
                "ApiEndpointDefinition",
                action.ControllerTypeInfo.Assembly.GetName().Name,
                sourceName,
                evidence,
                scopeCode,
                now);
        }

        var uiAssembliesScanned = 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic))
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x is not null).Cast<Type>().ToArray(); }

            var componentTypes = types
                .Where(x => !x.IsAbstract && typeof(ComponentBase).IsAssignableFrom(x))
                .ToArray();

            if (componentTypes.Length == 0)
                continue;

            uiAssembliesScanned++;

            foreach (var type in componentTypes)
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!LooksLikeBusinessAction(method.Name) || method.IsSpecialName)
                        continue;
                    if (method.GetCustomAttribute<SecurityFunctionDefinitionAttribute>() is not null)
                        continue;

                    var candidateKey = $"Candidate.Ui.{type.FullName}.{method.Name}";
                    seenUi.Add(candidateKey);
                    UpsertCandidate(
                        registry,
                        candidateKey,
                        $"Cần xác nhận thao tác giao diện {ToDisplayName(method.Name)}",
                        type.Name,
                        0,
                        type.Name,
                        method.Name,
                        "UiActionCandidate",
                        assembly.GetName().Name,
                        $"{type.FullName}.{method.Name}",
                        type.FullName ?? type.Name,
                        "Review",
                        now);
                }
            }
        }

        if (_endpointSources.Any())
        {
            foreach (var item in registry.Values.Where(x =>
                         (x.SourceType == "ApiEndpointDefinition" || x.SourceType == "ApiEndpointUnmapped") &&
                         !seenApi.Contains(x.FunctionKey) &&
                         x.LifecycleStatus == "PendingRegistration"))
            {
                item.LifecycleStatus = "PendingRetirement";
                item.ResolvedAt = null;
            }
        }

        if (uiAssembliesScanned > 0)
        {
            foreach (var item in registry.Values.Where(x =>
                         x.SourceType == "UiActionCandidate" &&
                         !seenUi.Contains(x.FunctionKey) &&
                         x.LifecycleStatus == "PendingRegistration"))
            {
                item.LifecycleStatus = "PendingRetirement";
                item.ResolvedAt = null;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return seenApi.Count + seenUi.Count;
    }

    private void UpsertCandidate(
        IDictionary<string, F03SecurityFunctionRegistryItem> registry,
        string key,
        string definition,
        string functionKey,
        int functionCode,
        string module,
        string action,
        string sourceType,
        string? sourceAssembly,
        string sourceTypeName,
        string evidence,
        string scopeCode,
        DateTime now)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(key + "|" + evidence + "|" + functionCode))).ToLowerInvariant();

        if (!registry.TryGetValue(key, out var item))
        {
            item = new F03SecurityFunctionRegistryItem
            {
                FunctionKey = key,
                FunctionCode = functionCode,
                DefinitionName = definition,
                ModuleCode = module,
                ActionCode = action,
                ScopeCode = scopeCode,
                LifecycleStatus = functionCode > 0 ? "Active" : "PendingRegistration",
                SourceType = sourceType,
                SourceAssembly = sourceAssembly,
                SourceTypeName = sourceTypeName,
                DefinitionHash = hash,
                FirstDiscoveredAt = now,
                LastSeenAt = now,
                IsIgnored = false
            };
            _db.SecurityFunctionRegistry.Add(item);
            registry[key] = item;
            return;
        }

        item.LastSeenAt = now;
        item.DefinitionHash = hash;
        item.DefinitionName = definition;
        item.FunctionCode = functionCode;
        item.ModuleCode = module;
        item.ActionCode = action;
        item.ScopeCode = scopeCode;
        item.SourceAssembly = sourceAssembly;
        item.SourceTypeName = sourceTypeName;

        if (item.LifecycleStatus != "Ignored" && item.LifecycleStatus != "Retired" && item.LifecycleStatus != "Replaced")
            item.LifecycleStatus = functionCode > 0 ? "Active" : "PendingRegistration";
    }

    private static string TrimControllerSuffix(string name) =>
        name.EndsWith("Controller", StringComparison.Ordinal) ? name[..^10] : name;

    private static bool LooksLikeBusinessAction(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName)) return false;
        if (methodName.EndsWith("Async", StringComparison.Ordinal))
            methodName = methodName[..^5];

        return CandidateActionWords.Any(word =>
            string.Equals(methodName, word, StringComparison.OrdinalIgnoreCase) ||
            methodName.StartsWith(word, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToDisplayName(string methodName)
    {
        if (methodName.EndsWith("Async", StringComparison.Ordinal))
            methodName = methodName[..^5];

        return methodName switch
        {
            "Create" or "Add" => "thêm mới",
            "Update" or "Edit" => "cập nhật",
            "Delete" or "Remove" => "xóa",
            "Approve" => "phê duyệt",
            "Reject" => "từ chối",
            "Cancel" => "hủy",
            "Submit" => "gửi xử lý",
            "Assign" or "Handover" => "bàn giao",
            "Transfer" => "điều chuyển",
            "Return" => "thu hồi",
            "Repair" => "lập phiếu sửa chữa",
            "Import" => "nhập dữ liệu",
            "ImportExcel" => "nhập dữ liệu Excel",
            "Export" => "xuất dữ liệu",
            "ExportExcel" => "xuất dữ liệu Excel",
            "Upload" => "tải lên",
            "Download" => "tải xuống",
            "DownloadTemplate" => "tải mẫu dữ liệu",
            "Print" => "in",
            "Sync" => "đồng bộ",
            "Calculate" or "Recalculate" => "tính toán",
            "Lock" => "khóa",
            "Unlock" => "mở khóa",
            "Reset" => "đặt lại",
            "Generate" => "tạo dữ liệu",
            "Send" => "gửi",
            "Publish" => "công bố",
            "Unpublish" => "hủy công bố",
            "Archive" => "lưu trữ",
            "Restore" => "khôi phục",
            "Execute" => "thực hiện",
            _ => ToVietnameseCompoundAction(methodName)
        };
    }

    private static string ToVietnameseCompoundAction(string methodName)
    {
        if (methodName.StartsWith("Export", StringComparison.OrdinalIgnoreCase))
            return "xuất " + ToVietnameseObject(methodName[6..]);
        if (methodName.StartsWith("Import", StringComparison.OrdinalIgnoreCase))
            return "nhập " + ToVietnameseObject(methodName[6..]);
        if (methodName.StartsWith("Download", StringComparison.OrdinalIgnoreCase))
            return "tải xuống " + ToVietnameseObject(methodName[8..]);
        if (methodName.StartsWith("Upload", StringComparison.OrdinalIgnoreCase))
            return "tải lên " + ToVietnameseObject(methodName[6..]);
        if (methodName.StartsWith("Print", StringComparison.OrdinalIgnoreCase))
            return "in " + ToVietnameseObject(methodName[5..]);

        return methodName;
    }

    private static string ToVietnameseObject(string value) => value switch
    {
        "Excel" => "dữ liệu Excel",
        "Template" => "mẫu dữ liệu",
        "Report" => "báo cáo",
        "File" => "tệp",
        _ => value
    };
}

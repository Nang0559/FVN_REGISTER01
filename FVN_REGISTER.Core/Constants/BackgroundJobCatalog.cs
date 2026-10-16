namespace FVN_REGISTER.Core.Constants;

/// <summary>Metadata of one schedulable background job.</summary>
/// <param name="Key">Stable identifier stored in dbo.F03BackgroundJobSchedules.JobKey.</param>
/// <param name="DefaultIntervalMinutes">Interval used when no DB row exists (or after "reset to default").</param>
/// <param name="AllowDisable">False for housekeeping jobs that must always run (security cleanup/discovery).</param>
public sealed record BackgroundJobDefinition(
    string Key,
    string DisplayName,
    string Description,
    int DefaultIntervalMinutes,
    bool AllowDisable = true);

/// <summary>
/// Single source of truth for the background jobs whose schedule can be changed from the admin UI.
/// Keep in sync with the seed in SQL/75_BackgroundJobSchedules.sql.
/// </summary>
public static class BackgroundJobCatalog
{
    public const string HrmSync = "hrm-sync";
    public const string EmailQueue = "email-queue";
    public const string Escalation = "escalation";
    public const string ActionItemLifecycle = "action-item-lifecycle";
    public const string EquipmentInspection = "equipment-inspection";
    public const string ExecutionReconciliation = "execution-reconciliation";
    public const string EndpointCredentialCleanup = "endpoint-credential-cleanup";
    public const string SecurityFunctionDiscovery = "security-function-discovery";

    public const int MinIntervalMinutes = 1;
    public const int MaxIntervalMinutes = 10080; // 7 days

    public static IReadOnlyList<BackgroundJobDefinition> All { get; } = new List<BackgroundJobDefinition>
    {
        new(HrmSync, "Đồng bộ HRM",
            "Import HRM, sync Department/Employee/..., provision User + Approver. Tác vụ nặng nhất.", 30),
        new(EmailQueue, "Gửi email trong hàng đợi", "Xử lý hàng đợi email.", 5),
        new(Escalation, "Leo thang phê duyệt (Leave/OT)", "Quét yêu cầu quá hạn phê duyệt và leo thang.", 15),
        new(ActionItemLifecycle, "Vòng đời Action Item", "Chuyển Action Item quá hạn, sửa dữ liệu mồ côi.", 10),
        new(EquipmentInspection, "Sinh lịch kiểm tra thiết bị", "Tạo task kiểm tra thiết bị định kỳ.", 10),
        new(ExecutionReconciliation, "Đối soát thực thi công", "Đối soát chấm công theo ExecutionPolicy.", 30),
        new(EndpointCredentialCleanup, "Dọn credential endpoint hết hạn",
            "Thu hồi credential hết thời gian grace (tác vụ bảo mật, không được tắt).", 15, AllowDisable: false),
        new(SecurityFunctionDiscovery, "Quét function bảo mật",
            "Đối chiếu SecurityFunction trong code với DB (không được tắt).", 360, AllowDisable: false),
    };

    public static BackgroundJobDefinition? Find(string? key)
        => string.IsNullOrWhiteSpace(key)
            ? null
            : All.FirstOrDefault(x => string.Equals(x.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
}

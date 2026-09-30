namespace FVN_REGISTER.Shared.Services.Language;

internal static class LanguageCatalogModules
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard.loading"] = "Đang nạp thông số hệ thống FCC...", ["dashboard.pending"] = "Đơn đang chờ tôi xử lý", ["dashboard.pendingCount"] = "{0} đơn gần nhất", ["dashboard.viewAll"] = "Xem tất cả", ["dashboard.level"] = "Cấp {0}",
        ["dashboard.leave"] = "Phép của tôi", ["dashboard.history"] = "Lịch sử", ["dashboard.entitled"] = "Được hưởng", ["dashboard.used"] = "Đã nghỉ", ["dashboard.remaining"] = "Còn lại", ["dashboard.days"] = "ngày", ["dashboard.nextLeave"] = "Sắp nghỉ", ["dashboard.upcomingDays"] = "Tổng các đợt nghỉ sắp tới: {0} ngày", ["dashboard.noUpcomingLeave"] = "Chưa có ngày nghỉ đã duyệt sắp tới.",
        ["dashboard.ot"] = "OT của tôi", ["dashboard.otWeek"] = "OT tuần", ["dashboard.otMonth"] = "OT tháng", ["dashboard.otYear"] = "OT năm", ["dashboard.otLimitWarning"] = "Có giới hạn OT đang gần chạm. Tuần: {0}h, tháng: {1}h, năm: {2}h.",
        ["dashboard.deptSituation"] = "Tình hình phòng ban — {0}", ["dashboard.managerOnly"] = "Chỉ hiển thị khi tài khoản có quyền quản lý.", ["dashboard.staff"] = "Nhân sự", ["dashboard.absent"] = "Đang vắng", ["dashboard.pending"] = "Chờ duyệt", ["dashboard.absenceRate"] = "Tỷ lệ vắng", ["dashboard.leaveStatistics"] = "Thống kê nghỉ phép phòng ban", ["dashboard.department"] = "Phòng ban", ["dashboard.present"] = "Đang có mặt", ["dashboard.leaveRequests"] = "Đơn nghỉ", ["dashboard.leaveRate"] = "Tỷ lệ nghỉ", ["dashboard.leaveType"] = "Nghỉ phép", ["dashboard.tripType"] = "Công tác"
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard.loading"] = "FCCシステム情報を読み込んでいます...", ["dashboard.pending"] = "処理待ちの申請", ["dashboard.pendingCount"] = "直近{0}件", ["dashboard.viewAll"] = "すべて表示", ["dashboard.level"] = "レベル {0}",
        ["dashboard.leave"] = "自分の休暇", ["dashboard.history"] = "履歴", ["dashboard.entitled"] = "付与日数", ["dashboard.used"] = "取得済み", ["dashboard.remaining"] = "残り", ["dashboard.days"] = "日", ["dashboard.nextLeave"] = "次の休暇", ["dashboard.upcomingDays"] = "今後の休暇合計: {0}日", ["dashboard.noUpcomingLeave"] = "今後の承認済み休暇はありません。",
        ["dashboard.ot"] = "自分の残業", ["dashboard.otWeek"] = "週残業", ["dashboard.otMonth"] = "月残業", ["dashboard.otYear"] = "年残業", ["dashboard.otLimitWarning"] = "残業上限に近づいています。週: {0}h、月: {1}h、年: {2}h。",
        ["dashboard.deptSituation"] = "部門状況 — {0}", ["dashboard.managerOnly"] = "管理権限を持つアカウントにのみ表示されます。", ["dashboard.staff"] = "社員数", ["dashboard.absent"] = "不在", ["dashboard.pending"] = "承認待ち", ["dashboard.absenceRate"] = "不在率", ["dashboard.leaveStatistics"] = "部門休暇統計", ["dashboard.department"] = "部門", ["dashboard.present"] = "出勤", ["dashboard.leaveRequests"] = "休暇申請", ["dashboard.leaveRate"] = "休暇率", ["dashboard.leaveType"] = "休暇", ["dashboard.tripType"] = "出張"
    };

    public static bool TryGet(LanguageCode language, string key, out string value) =>
        (language == LanguageCode.Ja ? Ja : Vi).TryGetValue(key, out value!);
}

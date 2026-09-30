namespace FVN_REGISTER.Shared.Services.Language;

internal static class LanguageCatalogAdditional
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.profile"] = "Hồ sơ của tôi", ["common.changePassword"] = "Đổi mật khẩu", ["common.sessions"] = "Thiết bị & phiên đăng nhập",
        ["leave.create"] = "Đăng ký nghỉ phép", ["leave.history"] = "Lịch sử đơn của tôi", ["common.employeeCode"] = "Mã NV",
        ["common.user"] = "Nhân viên FCC", ["route.forbidden"] = "Bạn không có quyền truy cập.", ["route.notFound"] = "Không tìm thấy trang này (404 Not Found)."
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.profile"] = "マイプロフィール", ["common.changePassword"] = "パスワード変更", ["common.sessions"] = "デバイスとログインセッション",
        ["leave.create"] = "休暇申請", ["leave.history"] = "自分の申請履歴", ["common.employeeCode"] = "社員番号",
        ["common.user"] = "FCC社員", ["route.forbidden"] = "このページにアクセスする権限がありません。", ["route.notFound"] = "ページが見つかりません（404）。"
    };

    public static bool TryGet(LanguageCode language, string key, out string value) =>
        (language == LanguageCode.Ja ? Ja : Vi).TryGetValue(key, out value!);
}

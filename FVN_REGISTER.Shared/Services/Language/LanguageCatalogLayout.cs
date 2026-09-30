namespace FVN_REGISTER.Shared.Services.Language;

/// <summary>
/// Layout/profile strings kept separately from the module catalogs so shared chrome
/// (profile menu, account information and session navigation) is never left untranslated.
/// </summary>
public static class LanguageCatalogLayout
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.user"] = "Người dùng",
        ["common.profile"] = "Hồ sơ",
        ["common.changePassword"] = "Đổi mật khẩu",
        ["common.sessions"] = "Phiên đăng nhập",
        ["common.employeeCode"] = "Mã nhân viên",
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.user"] = "ユーザー",
        ["common.profile"] = "プロフィール",
        ["common.changePassword"] = "パスワード変更",
        ["common.sessions"] = "ログインセッション",
        ["common.employeeCode"] = "社員番号",
    };

    public static bool TryGet(LanguageCode language, string key, out string value)
    {
        var source = language == LanguageCode.Ja ? Ja : Vi;
        return source.TryGetValue(key, out value!);
    }
}

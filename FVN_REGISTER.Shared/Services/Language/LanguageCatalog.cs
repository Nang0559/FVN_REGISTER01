namespace FVN_REGISTER.Shared.Services.Language;

public static class LanguageCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.save"] = "Lưu", ["common.cancel"] = "Hủy", ["common.close"] = "Đóng", ["common.search"] = "Tìm kiếm",
        ["common.refresh"] = "Làm mới", ["common.create"] = "Thêm", ["common.edit"] = "Sửa", ["common.delete"] = "Xóa",
        ["common.submit"] = "Gửi", ["common.approve"] = "Phê duyệt", ["common.reject"] = "Từ chối", ["common.confirm"] = "Xác nhận",
        ["common.print"] = "In", ["common.export"] = "Xuất", ["common.exportExcel"] = "Xuất Excel", ["common.back"] = "Quay lại",
        ["common.loading"] = "Đang tải...", ["common.noData"] = "Không có dữ liệu", ["common.yes"] = "Có", ["common.no"] = "Không",
        ["common.all"] = "Tất cả", ["common.detail"] = "Chi tiết", ["common.action"] = "Thao tác", ["common.status"] = "Trạng thái",
        ["common.startDate"] = "Từ ngày", ["common.endDate"] = "Đến ngày", ["common.employee"] = "Nhân viên", ["common.department"] = "Phòng ban",
        ["common.position"] = "Chức vụ", ["common.reason"] = "Lý do", ["common.note"] = "Ghi chú", ["common.attachment"] = "Tệp đính kèm",
        ["common.required"] = "Bắt buộc", ["common.success"] = "Thành công", ["common.error"] = "Lỗi", ["common.warning"] = "Cảnh báo",
        ["common.login"] = "Đăng nhập", ["common.logout"] = "Đăng xuất", ["common.language"] = "Ngôn ngữ",
        ["language.vi"] = "Tiếng Việt", ["language.ja"] = "日本語",
        ["nav.home"] = "Trang chủ", ["nav.dashboard"] = "Tổng quan", ["nav.leave"] = "Nghỉ phép", ["nav.overtime"] = "Làm thêm giờ",
        ["nav.trip"] = "Công tác", ["nav.attendance"] = "Chấm công", ["nav.equipment"] = "Thiết bị", ["nav.approval"] = "Phê duyệt",
        ["nav.execution"] = "Xác nhận công", ["nav.payroll"] = "Tính lương", ["nav.employee"] = "Nhân viên", ["nav.department"] = "Phòng ban",
        ["nav.security"] = "Bảo mật", ["nav.hrm"] = "HRM", ["nav.history"] = "Lịch sử", ["nav.report"] = "Báo cáo",
        ["status.pending"] = "Chờ xử lý", ["status.approved"] = "Đã phê duyệt", ["status.rejected"] = "Đã từ chối", ["status.cancelled"] = "Đã hủy",
        ["status.resolved"] = "Đã giải quyết", ["status.mismatch"] = "Lệch dữ liệu", ["status.awaitingConfirmation"] = "Chờ xác nhận",
        ["status.needMoreEvidence"] = "Cần bổ sung bằng chứng", ["status.expired"] = "Hết hạn",
        ["message.saveSuccess"] = "Lưu thành công.", ["message.saveFailed"] = "Lưu thất bại.", ["message.deleteSuccess"] = "Xóa thành công.",
        ["message.deleteFailed"] = "Xóa thất bại.", ["message.submitSuccess"] = "Gửi thành công.", ["message.approveSuccess"] = "Phê duyệt thành công.",
        ["message.rejectSuccess"] = "Từ chối thành công.", ["message.noPermission"] = "Bạn không có quyền thực hiện thao tác này.",
        ["message.loadFailed"] = "Không thể tải dữ liệu.", ["message.sessionExpired"] = "Phiên đăng nhập đã hết hạn.",
        ["validation.required"] = "Vui lòng nhập đầy đủ thông tin bắt buộc.", ["validation.invalidDate"] = "Ngày không hợp lệ.",
        ["validation.startAfterEnd"] = "Ngày bắt đầu không được lớn hơn ngày kết thúc.", ["validation.attachmentRequired"] = "Vui lòng đính kèm bằng chứng.",
        ["execution.confirmRequired"] = "Bạn có ngày công cần xác nhận.", ["execution.evidence"] = "Bằng chứng làm việc",
        ["execution.hrReview"] = "Nhân sự xử lý xác nhận công", ["execution.ok"] = "Nhân sự xác nhận đúng", ["execution.ng"] = "Nhân sự xác nhận không đúng",
        ["payroll.printGate"] = "Chưa thể in bảng công tính lương vì vẫn còn dữ liệu cần xác nhận."
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.save"] = "保存", ["common.cancel"] = "キャンセル", ["common.close"] = "閉じる", ["common.search"] = "検索",
        ["common.refresh"] = "更新", ["common.create"] = "追加", ["common.edit"] = "編集", ["common.delete"] = "削除",
        ["common.submit"] = "送信", ["common.approve"] = "承認", ["common.reject"] = "却下", ["common.confirm"] = "確認",
        ["common.print"] = "印刷", ["common.export"] = "出力", ["common.exportExcel"] = "Excel出力", ["common.back"] = "戻る",
        ["common.loading"] = "読み込み中...", ["common.noData"] = "データがありません", ["common.yes"] = "はい", ["common.no"] = "いいえ",
        ["common.all"] = "すべて", ["common.detail"] = "詳細", ["common.action"] = "操作", ["common.status"] = "ステータス",
        ["common.startDate"] = "開始日", ["common.endDate"] = "終了日", ["common.employee"] = "社員", ["common.department"] = "部署",
        ["common.position"] = "役職", ["common.reason"] = "理由", ["common.note"] = "備考", ["common.attachment"] = "添付ファイル",
        ["common.required"] = "必須", ["common.success"] = "成功", ["common.error"] = "エラー", ["common.warning"] = "警告",
        ["common.login"] = "ログイン", ["common.logout"] = "ログアウト", ["common.language"] = "言語",
        ["language.vi"] = "Tiếng Việt", ["language.ja"] = "日本語",
        ["nav.home"] = "ホーム", ["nav.dashboard"] = "ダッシュボード", ["nav.leave"] = "休暇", ["nav.overtime"] = "残業",
        ["nav.trip"] = "出張", ["nav.attendance"] = "勤怠", ["nav.equipment"] = "備品", ["nav.approval"] = "承認",
        ["nav.execution"] = "勤務実績確認", ["nav.payroll"] = "給与計算", ["nav.employee"] = "社員", ["nav.department"] = "部署",
        ["nav.security"] = "セキュリティ", ["nav.hrm"] = "HRM", ["nav.history"] = "履歴", ["nav.report"] = "レポート",
        ["status.pending"] = "処理待ち", ["status.approved"] = "承認済み", ["status.rejected"] = "却下済み", ["status.cancelled"] = "キャンセル済み",
        ["status.resolved"] = "解決済み", ["status.mismatch"] = "不一致", ["status.awaitingConfirmation"] = "確認待ち",
        ["status.needMoreEvidence"] = "証拠の追加が必要", ["status.expired"] = "期限切れ",
        ["message.saveSuccess"] = "保存しました。", ["message.saveFailed"] = "保存に失敗しました。", ["message.deleteSuccess"] = "削除しました。",
        ["message.deleteFailed"] = "削除に失敗しました。", ["message.submitSuccess"] = "送信しました。", ["message.approveSuccess"] = "承認しました。",
        ["message.rejectSuccess"] = "却下しました。", ["message.noPermission"] = "この操作を実行する権限がありません。",
        ["message.loadFailed"] = "データを読み込めませんでした。", ["message.sessionExpired"] = "ログインセッションの有効期限が切れました。",
        ["validation.required"] = "必須項目を入力してください。", ["validation.invalidDate"] = "日付が正しくありません。",
        ["validation.startAfterEnd"] = "開始日は終了日より後にできません。", ["validation.attachmentRequired"] = "証拠ファイルを添付してください。",
        ["execution.confirmRequired"] = "確認が必要な勤務実績があります。", ["execution.evidence"] = "勤務証拠",
        ["execution.hrReview"] = "人事による勤務実績確認", ["execution.ok"] = "人事確認済み（正しい）", ["execution.ng"] = "人事確認済み（不正確）",
        ["payroll.printGate"] = "未確認のデータが残っているため、給与計算用の勤務表を印刷できません。"
    };

    public static string Get(LanguageCode language, string key) =>
        (language == LanguageCode.Ja ? Ja : Vi).TryGetValue(key, out var value) ? value : key;
}

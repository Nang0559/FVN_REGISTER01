namespace FVN_REGISTER.Shared.Services.Language;

public static class LanguageCatalogCalendar
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["calendar.feedback.systemData"] = "Dữ liệu chấm công hệ thống", ["calendar.feedback.otActual"] = "OT thực tế", ["calendar.feedback.otRecognized"] = "OT được ghi nhận", ["calendar.feedback.reconciliation"] = "Hồ sơ đối soát",
        ["calendar.feedback.pending"] = "Phản hồi cho ngày này đã được gửi trước đó và đang chờ Nhân sự xử lý.", ["calendar.feedback.sentComment"] = "Ý kiến đã gửi", ["calendar.feedback.title"] = "Phản hồi Nhân sự", ["calendar.feedback.decision"] = "Kết luận phản hồi",
        ["calendar.feedback.confirmed"] = "Xác nhận dữ liệu chấm công / OT thực tế là đúng", ["calendar.feedback.rejected"] = "Dữ liệu chấm công / OT thực tế không đúng", ["calendar.feedback.comment"] = "Ý kiến / giải trình", ["calendar.feedback.commentHelp"] = "Nêu rõ nội dung cần Nhân sự kiểm tra hoặc điều chỉnh.",
        ["calendar.feedback.evidence"] = "Bằng chứng đính kèm", ["calendar.feedback.evidenceRequired"] = "Vui lòng đính kèm file bằng chứng (ảnh, PDF hoặc tài liệu liên quan).", ["calendar.feedback.evidenceOptional"] = "Có thể đính kèm file bằng chứng nếu cần.", ["calendar.feedback.sending"] = "Đang gửi...", ["calendar.feedback.send"] = "Gửi phản hồi Nhân sự",
        ["calendar.feedback.loadFailed"] = "Không tải được hồ sơ đối soát.", ["calendar.feedback.commentRequired"] = "Vui lòng nhập ý kiến / giải trình.", ["calendar.feedback.attachmentRequired"] = "Vui lòng đính kèm file bằng chứng.", ["calendar.feedback.submitFailed"] = "Không gửi được phản hồi Nhân sự.", ["calendar.feedback.sent"] = "Đã gửi phản hồi ngày công cho Nhân sự.",
        ["calendar.feedback.uploadFailed"] = "Phản hồi đã gửi nhưng không upload được file bằng chứng. Bạn có thể bổ sung evidence sau.", ["calendar.feedback.evidenceSaveFailed"] = "Phản hồi đã gửi nhưng chưa lưu được thông tin file bằng chứng.", ["calendar.feedback.evidenceTitle"] = "Bằng chứng phản hồi ngày {0}.", ["calendar.feedback.hoursMinutes"] = "{0} giờ {1} phút", ["calendar.feedback.minutes"] = "{0} phút",
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["calendar.feedback.systemData"] = "システム勤怠データ", ["calendar.feedback.otActual"] = "実績残業", ["calendar.feedback.otRecognized"] = "認識済み残業", ["calendar.feedback.reconciliation"] = "照合レコード",
        ["calendar.feedback.pending"] = "この日の回答はすでに送信され、人事の確認待ちです。", ["calendar.feedback.sentComment"] = "送信済みコメント", ["calendar.feedback.title"] = "人事への回答", ["calendar.feedback.decision"] = "回答結果",
        ["calendar.feedback.confirmed"] = "勤怠・実績残業データが正しいことを確認する", ["calendar.feedback.rejected"] = "勤怠・実績残業データが正しくない", ["calendar.feedback.comment"] = "コメント / 説明", ["calendar.feedback.commentHelp"] = "人事に確認または修正してほしい内容を具体的に記入してください。",
        ["calendar.feedback.evidence"] = "添付証拠", ["calendar.feedback.evidenceRequired"] = "証拠ファイル（画像、PDF、関連資料）を添付してください。", ["calendar.feedback.evidenceOptional"] = "必要に応じて証拠ファイルを添付できます。", ["calendar.feedback.sending"] = "送信中...", ["calendar.feedback.send"] = "人事へ回答を送信",
        ["calendar.feedback.loadFailed"] = "照合レコードを読み込めませんでした。", ["calendar.feedback.commentRequired"] = "コメント / 説明を入力してください。", ["calendar.feedback.attachmentRequired"] = "証拠ファイルを添付してください。", ["calendar.feedback.submitFailed"] = "人事への回答を送信できませんでした。", ["calendar.feedback.sent"] = "勤怠に関する回答を人事へ送信しました。",
        ["calendar.feedback.uploadFailed"] = "回答は送信されましたが、証拠ファイルをアップロードできませんでした。後から証拠を追加できます。", ["calendar.feedback.evidenceSaveFailed"] = "回答は送信されましたが、証拠ファイル情報を保存できませんでした。", ["calendar.feedback.evidenceTitle"] = "{0} の回答証拠。", ["calendar.feedback.hoursMinutes"] = "{0}時間 {1}分", ["calendar.feedback.minutes"] = "{0}分",
    };

    public static bool TryGet(LanguageCode language, string key, out string value)
    {
        var source = language == LanguageCode.Ja ? Ja : Vi;
        return source.TryGetValue(key, out value!);
    }
}

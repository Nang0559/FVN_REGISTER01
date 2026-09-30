namespace FVN_REGISTER.Shared.Services.Language;

public static class LanguageCatalogHelp
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["help.HOME_WORKSPACE.title"] = "Workspace cá nhân",
        ["help.HOME_WORKSPACE.summary"] = "Trang chủ gom việc cần làm, thông báo, phê duyệt và các module mà tài khoản được phép sử dụng.",
        ["help.HOME_WORKSPACE.flow"] = "USER → ACTION CENTER → MY WORK → NOTIFICATION/APPROVAL → MODULE",
        ["help.HOME_WORKSPACE.steps"] = "Kiểm tra việc cần làm.|Mở thông báo hoặc hồ sơ cần xử lý.|Dùng các ô module theo quyền được cấp.|Xem tổng quan quản lý chỉ khi có capability tương ứng.",
        ["help.HOME_WORKSPACE.example"] = "Nhân viên có LeaveView nhưng không có LeaveApprove chỉ thấy thao tác cá nhân; ẩn nút không phải cơ chế bảo mật.",
        ["help.HOME_WORKSPACE.security"] = "Capability và data scope được kiểm tra ở server; UI chỉ phản ánh quyền.",

        ["help.APPROVAL_INBOX.title"] = "Hộp việc phê duyệt",
        ["help.APPROVAL_INBOX.summary"] = "Một nơi để xem và xử lý Leave, OT, Trip và Equipment thuộc phạm vi được phép.",
        ["help.APPROVAL_INBOX.flow"] = "PENDING → SCOPE CHECK → REVIEW → APPROVE/REJECT → AUDIT",
        ["help.APPROVAL_INBOX.steps"] = "Lọc theo module.|Mở chi tiết.|Chọn các mục cùng cấp xử lý.|Approve/Reject; server kiểm tra lại scope trước workflow.",
        ["help.APPROVAL_INBOX.example"] = "Nếu chọn 5 đơn nhưng 1 đơn vượt scope, cả batch bị từ chối thay vì silently skip.",
        ["help.APPROVAL_INBOX.security"] = "Approve là capability riêng; Approve không tự động cấp All.",

        ["help.EQUIPMENT_WORKSPACE.title"] = "Thiết bị",
        ["help.EQUIPMENT_WORKSPACE.summary"] = "Quản lý tài sản thiết bị theo schema, import dữ liệu, checklist, sửa chữa, bàn giao, điều chuyển và phê duyệt; tất cả dùng cùng một Help.",
        ["help.EQUIPMENT_WORKSPACE.flow"] = "SCHEMA → DATA → VALIDATE → REVIEW → APPROVAL → COMMIT → AUDIT",
        ["help.EQUIPMENT_WORKSPACE.steps"] = "Chọn phòng ban và schema được phép sử dụng.|Nhập hoặc import dữ liệu.|Kiểm tra preview và lỗi.|Gửi yêu cầu theo workflow hiện hành.|Theo dõi phê duyệt, lịch sử và trạng thái thiết bị.",
        ["help.EQUIPMENT_WORKSPACE.example"] = "Một thiết bị có thể phát sinh thêm mới, sửa chữa, điều chuyển, thay người sử dụng hoặc thanh lý; người duyệt xem tổng thể thiết bị cùng lịch sử thay đổi và checklist nếu có.",
        ["help.EQUIPMENT_WORKSPACE.security"] = "Capability và data scope được kiểm tra ở server; Help chỉ hướng dẫn, không cấp quyền.",

        ["help.EQUIPMENT_SCHEMA.title"] = "Schema thiết bị",
        ["help.EQUIPMENT_SCHEMA.summary"] = "Schema định nghĩa cấu trúc dữ liệu dùng cho thiết bị và có thể được dùng lại cho các loại dữ liệu khác khi nghiệp vụ cho phép.",
        ["help.EQUIPMENT_SCHEMA.flow"] = "CREATE/COPY → DEFINE FIELDS → VALIDATE → PUBLISH/USE",
        ["help.EQUIPMENT_SCHEMA.steps"] = "Chọn schema của phòng ban hoặc schema được phép sử dụng.|Tạo mới hoặc nhân bản schema của người khác.|Chỉnh sửa bản sao của mình; không sửa schema do người khác sở hữu.|Xác nhận kiểu dữ liệu và cột bắt buộc.|Lưu và sử dụng schema theo quyền.",
        ["help.EQUIPMENT_SCHEMA.example"] = "Người cùng phòng có quyền import Excel có thể xem schema của phòng, nhân bản schema của đồng nghiệp và chỉnh bản sao của mình.",
        ["help.EQUIPMENT_SCHEMA.security"] = "Quyền tạo/sửa/xóa schema phải được kiểm tra theo owner/department và capability; không dùng UI để bảo vệ thay cho server.",

        ["help.EQUIPMENT_IMPORT.title"] = "Import dữ liệu thiết bị",
        ["help.EQUIPMENT_IMPORT.summary"] = "Import Excel dùng schema đã được phép sử dụng; hệ thống preview và kiểm tra dữ liệu trước khi ghi chính thức.",
        ["help.EQUIPMENT_IMPORT.flow"] = "SELECT FILE → READ HEADER → MAP SCHEMA → PREVIEW → VALIDATE → COMMIT",
        ["help.EQUIPMENT_IMPORT.steps"] = "Chọn schema.|Chọn file Excel.|Đọc header và đối chiếu cột.|Kiểm tra dữ liệu từng dòng.|Sửa lỗi rồi preview lại.|Commit khi hợp lệ.",
        ["help.EQUIPMENT_IMPORT.example"] = "Có thể dùng file mẫu để tự nhận diện tên cột, sau đó người dùng hiệu chỉnh mapping và kiểu dữ liệu trước khi import.",
        ["help.EQUIPMENT_IMPORT.security"] = "Import phải kiểm tra capability, scope phòng ban và schema được phép sử dụng ở server.",

        ["help.EQUIPMENT_CHECKLIST.title"] = "Checklist thiết bị",
        ["help.EQUIPMENT_CHECKLIST.summary"] = "Checklist được áp dụng theo loại thiết bị và chu kỳ ngày/tuần/tháng/quý/năm khi loại thiết bị có cấu hình tương ứng.",
        ["help.EQUIPMENT_CHECKLIST.flow"] = "DEVICE TYPE → CHECKLIST TEMPLATE → SCHEDULE → EXECUTE → REVIEW → AUDIT",
        ["help.EQUIPMENT_CHECKLIST.steps"] = "Xác định loại thiết bị.|Xem checklist được áp dụng.|Thực hiện theo chu kỳ.|Ghi nhận kết quả và bằng chứng nếu cần.|Theo dõi lịch sử checklist tại thiết bị.",
        ["help.EQUIPMENT_CHECKLIST.example"] = "Laptop có thể có checklist hàng tháng; server có thể có checklist khác theo loại và chính sách IT.",
        ["help.EQUIPMENT_CHECKLIST.security"] = "Checklist không thay đổi quyền Equipment/Approval; việc thực hiện và phê duyệt được kiểm tra theo capability và scope.",

        ["help.EQUIPMENT_REPAIR.title"] = "Sửa chữa thiết bị",
        ["help.EQUIPMENT_REPAIR.summary"] = "Yêu cầu sửa chữa được gắn với thiết bị, trạng thái, lịch sử xử lý và approval khi policy yêu cầu.",
        ["help.EQUIPMENT_REPAIR.flow"] = "REQUEST → REVIEW → APPROVAL → REPAIR → COMPLETE → HISTORY",
        ["help.EQUIPMENT_REPAIR.steps"] = "Chọn thiết bị.|Mô tả sự cố và bằng chứng.|Gửi yêu cầu.|Theo dõi xử lý và phê duyệt.|Ghi nhận hoàn thành và lịch sử.",
        ["help.EQUIPMENT_REPAIR.example"] = "Thay linh kiện hoặc sửa chữa làm thay đổi thông tin thiết bị phải được ghi nhận trong lịch sử thay đổi.",
        ["help.EQUIPMENT_REPAIR.security"] = "Không tự thay đổi tài sản ngoài scope; mọi thao tác nhạy cảm phải được service kiểm tra.",

        ["help.EQUIPMENT_HANDOVER.title"] = "Bàn giao và điều chuyển thiết bị",
        ["help.EQUIPMENT_HANDOVER.summary"] = "Bàn giao, đổi người sử dụng và chuyển bộ thiết bị được quản lý như thay đổi có lịch sử và approval theo policy.",
        ["help.EQUIPMENT_HANDOVER.flow"] = "REQUEST → SNAPSHOT → APPROVAL → HANDOVER/TRANSFER → AUDIT",
        ["help.EQUIPMENT_HANDOVER.steps"] = "Chọn thiết bị và người nhận/đơn vị đích.|Xem snapshot trước thay đổi.|Gửi phê duyệt.|Thực hiện bàn giao/điều chuyển sau khi được duyệt.|Lưu lịch sử và bằng chứng.",
        ["help.EQUIPMENT_HANDOVER.example"] = "Người duyệt nhìn thấy thiết bị hiện tại, thay đổi đề nghị và lịch sử liên quan trước khi quyết định.",
        ["help.EQUIPMENT_HANDOVER.security"] = "Approval và data scope được kiểm tra server-side; thay đổi phải có audit.",

        ["help.PUBLIC_INFORMATION_CMS.title"] = "CMS thông tin công khai",
        ["help.PUBLIC_INFORMATION_CMS.summary"] = "Quản trị thông báo, quy định, hướng dẫn và chính sách theo Draft → Published → Archived.",
        ["help.PUBLIC_INFORMATION_CMS.flow"] = "DRAFT → PREVIEW → PUBLISH → PUBLIC READ → ARCHIVE",
        ["help.PUBLIC_INFORMATION_CMS.steps"] = "Tạo bản nháp.|Nhập nội dung và thời gian hiệu lực.|Preview.|Publish.|Archive khi kết thúc.",
        ["help.PUBLIC_INFORMATION_CMS.example"] = "Quy định có EffectiveFrom 01/10 chỉ xuất hiện trong cửa sổ hiệu lực đã cấu hình.",
        ["help.PUBLIC_INFORMATION_CMS.security"] = "Chỉ PublicInformationManage được thay đổi; endpoint public chỉ trả Published hợp lệ.",

        ["help.SECURITY_CENTER.title"] = "Security Center",
        ["help.SECURITY_CENTER.summary"] = "Quản lý role, function/action, effective permission và audit; capability tách khỏi data scope.",
        ["help.SECURITY_CENTER.flow"] = "USER → ROLE → FUNCTION/ACTION → SCOPE → AUDIT",
        ["help.SECURITY_CENTER.steps"] = "Xem effective permission.|Kiểm tra Role → Function/Action.|Gán role/function.|Kiểm tra audit.",
        ["help.SECURITY_CENTER.example"] = "LeaveApprove + Department cho phép duyệt trong phạm vi phòng ban; không đồng nghĩa xem toàn công ty.",
        ["help.SECURITY_CENTER.security"] = "DB là authority; JWT/UI chỉ hỗ trợ UX. Command nhạy cảm phải kiểm tra lại ở service.",

        ["help.LEAVE.title"] = "Nghỉ phép",
        ["help.LEAVE.summary"] = "Đăng ký, chỉnh sửa, hủy và phê duyệt nghỉ phép theo capability và scope.",
        ["help.LEAVE.flow"] = "CREATE → SUBMIT → PENDING → APPROVE/REJECT → HISTORY",
        ["help.LEAVE.steps"] = "Tạo đơn.|Kiểm tra ngày và loại nghỉ.|Submit.|Theo dõi lịch sử hoặc Inbox.",
        ["help.LEAVE.example"] = "Own chỉ xử lý dữ liệu của chính mình; Department mở rộng theo phòng ban.",
        ["help.LEAVE.security"] = "Create/Edit/Cancel/Approve là capability độc lập.",

        ["help.OT.title"] = "Làm thêm giờ",
        ["help.OT.summary"] = "Đăng ký OT và kiểm tra hạn mức ngày/tuần/tháng/năm trước khi gửi duyệt.",
        ["help.OT.flow"] = "NHẬP OT → KIỂM TRA NGÀY → TUẦN → THÁNG → NĂM → SUBMIT → APPROVAL → RECONCILE",
        ["help.OT.steps"] = "Chọn ngày, loại OT và số giờ.|Hệ thống kiểm tra hạn mức ngày.|Kiểm tra tổng OT trong tuần Thứ 2 → Chủ nhật nếu Weekly rule được cấu hình.|Kiểm tra hạn mức tháng và năm.|Nếu vượt ngưỡng năm tiêu chuẩn, xử lý theo policy đặc biệt; vượt ngưỡng tối đa thì không đăng ký thêm.|Submit và theo dõi duyệt/đối soát.",
        ["help.OT.example"] = "Dashboard hiển thị OT tuần/tháng/năm đã Approved. Request Pending vẫn được tính khi server kiểm tra để tránh đăng ký chồng vượt hạn mức.",
        ["help.OT.security"] = "OTCreate/OTEdit/OTApprove là capability độc lập. Hạn mức lấy từ F03OTLimitRules theo Dept + Position > Dept > Position > toàn công ty. Không tự đặt hạn mức tuần nếu chưa có rule.",

        ["help.TRIP.title"] = "Công tác",
        ["help.TRIP.summary"] = "Quản lý công tác theo workflow và scope.",
        ["help.TRIP.flow"] = "CREATE → SUBMIT → APPROVAL → COMPLETE",
        ["help.TRIP.steps"] = "Tạo kế hoạch.|Kiểm tra người đi/phòng ban.|Submit.|Theo dõi Approval Inbox.",
        ["help.TRIP.example"] = "TripCreate chỉ cho target scope hợp lệ; IsAdmin không tự động mở rộng dữ liệu.",
        ["help.TRIP.security"] = "TripCreate/TripEdit/TripApprove là capability độc lập.",
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["help.HOME_WORKSPACE.title"] = "個人ワークスペース",
        ["help.HOME_WORKSPACE.summary"] = "ホームでは、対応事項、通知、承認、およびアカウントに許可された各モジュールをまとめて確認できます。",
        ["help.HOME_WORKSPACE.flow"] = "USER → ACTION CENTER → MY WORK → NOTIFICATION/APPROVAL → MODULE",
        ["help.HOME_WORKSPACE.steps"] = "対応事項を確認する。|通知または処理対象を開く。|付与された権限に応じてモジュールを利用する。|必要な権限がある場合のみ管理概要を確認する。",
        ["help.HOME_WORKSPACE.example"] = "LeaveView があり LeaveApprove がない社員には本人向け操作だけが表示されます。ボタンを隠すことはセキュリティ対策ではありません。",
        ["help.HOME_WORKSPACE.security"] = "Capability と data scope はサーバーで検証し、UI は権限を表示するだけです。",

        ["help.APPROVAL_INBOX.title"] = "承認受信箱",
        ["help.APPROVAL_INBOX.summary"] = "許可された範囲の Leave、OT、Trip、Equipment を確認・処理するための共通画面です。",
        ["help.APPROVAL_INBOX.flow"] = "PENDING → SCOPE CHECK → REVIEW → APPROVE/REJECT → AUDIT",
        ["help.APPROVAL_INBOX.steps"] = "モジュールで絞り込む。|詳細を開く。|同じ処理レベルの項目を選択する。|承認/却下前にサーバーが再度 scope を検証する。",
        ["help.APPROVAL_INBOX.example"] = "5件を選択して1件でも scope 外なら、黙って除外せず batch 全体を拒否します。",
        ["help.APPROVAL_INBOX.security"] = "Approve は独立した capability であり、Approve が All 権限を意味するわけではありません。",

        ["help.EQUIPMENT_WORKSPACE.title"] = "設備",
        ["help.EQUIPMENT_WORKSPACE.summary"] = "設備資産を schema、データ取込、checklist、修理、引渡し、移管、承認まで一貫して管理します。",
        ["help.EQUIPMENT_WORKSPACE.flow"] = "SCHEMA → DATA → VALIDATE → REVIEW → APPROVAL → COMMIT → AUDIT",
        ["help.EQUIPMENT_WORKSPACE.steps"] = "利用可能な部署と schema を選択する。|データを入力または import する。|preview とエラーを確認する。|現在の workflow に従って申請する。|承認、履歴、設備状態を確認する。",
        ["help.EQUIPMENT_WORKSPACE.example"] = "設備には新規登録、修理、移管、利用者変更、廃棄などが発生します。承認者は必要に応じて設備全体、変更履歴、checklist を確認します。",
        ["help.EQUIPMENT_WORKSPACE.security"] = "Capability と data scope はサーバーで検証します。Help は案内のみで権限を付与しません。",

        ["help.EQUIPMENT_SCHEMA.title"] = "設備 schema",
        ["help.EQUIPMENT_SCHEMA.summary"] = "Schema は設備データの構造を定義し、業務上許可される場合は他のデータ種別にも再利用できます。",
        ["help.EQUIPMENT_SCHEMA.flow"] = "CREATE/COPY → DEFINE FIELDS → VALIDATE → PUBLISH/USE",
        ["help.EQUIPMENT_SCHEMA.steps"] = "部署または利用許可された schema を選択する。|新規作成または他者の schema をコピーする。|自分のコピーだけを編集する。|データ型と必須列を確認する。|権限に従って保存・利用する。",
        ["help.EQUIPMENT_SCHEMA.example"] = "Excel import 権限を持つ同じ部署の社員は部署 schema を参照し、同僚の schema をコピーして自分のコピーを編集できます。",
        ["help.EQUIPMENT_SCHEMA.security"] = "Schema の作成・編集・削除は owner/department と capability に基づいてサーバーで検証します。UI だけで保護しません。",

        ["help.EQUIPMENT_IMPORT.title"] = "設備データの import",
        ["help.EQUIPMENT_IMPORT.summary"] = "許可された schema を使用して Excel を import し、正式登録前に preview とデータ検証を行います。",
        ["help.EQUIPMENT_IMPORT.flow"] = "SELECT FILE → READ HEADER → MAP SCHEMA → PREVIEW → VALIDATE → COMMIT",
        ["help.EQUIPMENT_IMPORT.steps"] = "Schema を選択する。|Excel ファイルを選択する。|Header を読み込み列を照合する。|各行を検証する。|エラーを修正して再 preview する。|有効になったら commit する。",
        ["help.EQUIPMENT_IMPORT.example"] = "サンプルファイルで列名を自動認識し、その後 mapping とデータ型を調整して import できます。",
        ["help.EQUIPMENT_IMPORT.security"] = "Import 時は capability、部署 scope、利用可能な schema をサーバーで検証します。",

        ["help.EQUIPMENT_CHECKLIST.title"] = "設備 checklist",
        ["help.EQUIPMENT_CHECKLIST.summary"] = "設備種別の設定に応じて、日次・週次・月次・四半期・年次の checklist を適用します。",
        ["help.EQUIPMENT_CHECKLIST.flow"] = "DEVICE TYPE → CHECKLIST TEMPLATE → SCHEDULE → EXECUTE → REVIEW → AUDIT",
        ["help.EQUIPMENT_CHECKLIST.steps"] = "設備種別を確認する。|適用される checklist を確認する。|周期に従って実施する。|必要に応じて結果と証拠を記録する。|設備の checklist 履歴を確認する。",
        ["help.EQUIPMENT_CHECKLIST.example"] = "Laptop は月次、server は IT ポリシーに応じて別の checklist を持つことができます。",
        ["help.EQUIPMENT_CHECKLIST.security"] = "Checklist は Equipment/Approval 権限を変更しません。実施・承認は capability と scope に基づいて検証します。",

        ["help.EQUIPMENT_REPAIR.title"] = "設備修理",
        ["help.EQUIPMENT_REPAIR.summary"] = "修理依頼は設備、状態、処理履歴と関連付けられ、policy に応じて承認を行います。",
        ["help.EQUIPMENT_REPAIR.flow"] = "REQUEST → REVIEW → APPROVAL → REPAIR → COMPLETE → HISTORY",
        ["help.EQUIPMENT_REPAIR.steps"] = "設備を選択する。|障害内容と証拠を入力する。|依頼を送信する。|処理と承認を追跡する。|完了と履歴を記録する。",
        ["help.EQUIPMENT_REPAIR.example"] = "部品交換や修理で設備情報が変わる場合は、変更履歴に記録します。",
        ["help.EQUIPMENT_REPAIR.security"] = "scope 外の資産を直接変更せず、機密操作は service で再検証します。",

        ["help.EQUIPMENT_HANDOVER.title"] = "設備の引渡し・移管",
        ["help.EQUIPMENT_HANDOVER.summary"] = "引渡し、利用者変更、設備セットの移管は、履歴と policy に基づく承認を伴う変更として管理します。",
        ["help.EQUIPMENT_HANDOVER.flow"] = "REQUEST → SNAPSHOT → APPROVAL → HANDOVER/TRANSFER → AUDIT",
        ["help.EQUIPMENT_HANDOVER.steps"] = "設備と受領者/移管先を選択する。|変更前 snapshot を確認する。|承認を申請する。|承認後に引渡し/移管を実施する。|履歴と証拠を保存する。",
        ["help.EQUIPMENT_HANDOVER.example"] = "承認者は現在の設備情報、申請された変更、関連履歴を確認してから判断します。",
        ["help.EQUIPMENT_HANDOVER.security"] = "Approval と data scope はサーバーで検証し、変更には audit を残します。",

        ["help.PUBLIC_INFORMATION_CMS.title"] = "公開情報 CMS",
        ["help.PUBLIC_INFORMATION_CMS.summary"] = "通知、規定、ガイド、ポリシーを Draft → Published → Archived で管理します。",
        ["help.PUBLIC_INFORMATION_CMS.flow"] = "DRAFT → PREVIEW → PUBLISH → PUBLIC READ → ARCHIVE",
        ["help.PUBLIC_INFORMATION_CMS.steps"] = "下書きを作成する。|内容と有効期間を入力する。|Preview する。|Publish する。|終了時に Archive する。",
        ["help.PUBLIC_INFORMATION_CMS.example"] = "EffectiveFrom が 10/01 の規定は、設定された有効期間内だけ公開されます。",
        ["help.PUBLIC_INFORMATION_CMS.security"] = "変更できるのは PublicInformationManage の権限者だけで、public endpoint は有効な Published のみ返します。",

        ["help.SECURITY_CENTER.title"] = "Security Center",
        ["help.SECURITY_CENTER.summary"] = "Role、function/action、effective permission、audit を管理し、capability と data scope を分離します。",
        ["help.SECURITY_CENTER.flow"] = "USER → ROLE → FUNCTION/ACTION → SCOPE → AUDIT",
        ["help.SECURITY_CENTER.steps"] = "Effective permission を確認する。|Role → Function/Action を確認する。|Role/function を割り当てる。|Audit を確認する。",
        ["help.SECURITY_CENTER.example"] = "LeaveApprove + Department は部署範囲での承認を許可しますが、全社データ閲覧を意味しません。",
        ["help.SECURITY_CENTER.security"] = "DB が authority です。JWT/UI は UX を補助するだけで、重要 command は service で再検証します。",

        ["help.LEAVE.title"] = "休暇",
        ["help.LEAVE.summary"] = "Capability と scope に従って休暇の申請、編集、取消、承認を行います。",
        ["help.LEAVE.flow"] = "CREATE → SUBMIT → PENDING → APPROVE/REJECT → HISTORY",
        ["help.LEAVE.steps"] = "申請を作成する。|日付と休暇種別を確認する。|Submit する。|履歴または Inbox で確認する。",
        ["help.LEAVE.example"] = "Own は本人データのみ、Department は部署範囲まで処理できます。",
        ["help.LEAVE.security"] = "Create/Edit/Cancel/Approve は独立した capability です。",

        ["help.OT.title"] = "残業",
        ["help.OT.summary"] = "承認申請前に日・週・月・年の上限を確認して残業を申請します。",
        ["help.OT.flow"] = "NHẬP OT → KIỂM TRA NGÀY → TUẦN → THÁNG → NĂM → SUBMIT → APPROVAL → RECONCILE",
        ["help.OT.steps"] = "日付、残業種別、時間を選択する。|日次上限を確認する。|Weekly rule がある場合は月曜から日曜の週合計を確認する。|月次・年次上限を確認する。|標準年次上限を超える場合は特別 policy に従い、最大上限を超える登録はできません。|Submit して承認/照合を追跡する。",
        ["help.OT.example"] = "Dashboard は Approved の週/月/年 OT を表示します。Pending request もサーバー側の上限確認に含め、重複超過を防ぎます。",
        ["help.OT.security"] = "OTCreate/OTEdit/OTApprove は独立した capability です。上限は F03OTLimitRules の Dept + Position > Dept > Position > 全社の優先順位で取得します。rule がなければ週上限を自動設定しません。",

        ["help.TRIP.title"] = "出張",
        ["help.TRIP.summary"] = "Workflow と scope に従って出張を管理します。",
        ["help.TRIP.flow"] = "CREATE → SUBMIT → APPROVAL → COMPLETE",
        ["help.TRIP.steps"] = "計画を作成する。|参加者と部署を確認する。|Submit する。|Approval Inbox を確認する。",
        ["help.TRIP.example"] = "TripCreate は有効な target scope に限定され、IsAdmin だけでデータ範囲が拡大することはありません。",
        ["help.TRIP.security"] = "TripCreate/TripEdit/TripApprove は独立した capability です。",
    };

    public static bool TryGet(LanguageCode language, string key, out string value)
    {
        var source = language == LanguageCode.Ja ? Ja : Vi;
        return source.TryGetValue(key, out value!);
    }

    public static string[] Steps(LanguageCode language, string featureCode)
        => Get(language, $"help.{featureCode}.steps").Split('|', StringSplitOptions.RemoveEmptyEntries);

    public static string Get(LanguageCode language, string key)
        => TryGet(language, key, out var value) ? value : key;
}

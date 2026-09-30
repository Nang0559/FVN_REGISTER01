namespace FVN_REGISTER.Shared.Services.Language;

internal static class LanguageCatalogAdditional
{
    private static readonly IReadOnlyDictionary<string, string> Vi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.profile"] = "Hồ sơ của tôi", ["common.changePassword"] = "Đổi mật khẩu", ["common.sessions"] = "Thiết bị & phiên đăng nhập",
        ["common.employeeCode"] = "Mã NV", ["common.user"] = "Nhân viên FCC",
        ["route.forbidden"] = "Bạn không có quyền truy cập.", ["route.notFound"] = "Không tìm thấy trang này (404 Not Found).",
        ["nav.workCalendar"] = "Lịch làm việc", ["nav.executionReconciliation"] = "Đối soát thực tế", ["nav.workInbox"] = "Trung tâm công việc",
        ["nav.public"] = "Công khai", ["nav.publicInformation"] = "Thông tin công khai", ["nav.publicForms"] = "Biểu mẫu đăng ký", ["nav.publicFormsManage"] = "Quản lý biểu mẫu đăng ký",
        ["nav.accessChange"] = "Thay đổi quyền", ["nav.accessChangeRequest"] = "Phiếu thay đổi quyền / bàn giao",
        ["nav.approval"] = "Phê duyệt", ["nav.approvalPending"] = "Danh sách chờ duyệt",
        ["nav.leaveOverview"] = "Tổng quan", ["nav.leaveCreate"] = "Tạo đơn mới", ["nav.leaveHistory"] = "Lịch sử nghỉ phép",
        ["nav.ot"] = "Làm thêm giờ (OT)", ["nav.otOverview"] = "Tổng quan", ["nav.otCreate"] = "Đăng ký OT", ["nav.otHistory"] = "Lịch sử OT",
        ["nav.tripOverview"] = "Tổng quan", ["nav.tripCreate"] = "Đăng ký công tác", ["nav.tripHistory"] = "Lịch sử công tác",
        ["nav.endpointGovernance"] = "Endpoint Governance", ["nav.endpointCatalog"] = "Danh mục phần mềm / dịch vụ", ["nav.endpointInventory"] = "Danh mục Endpoint", ["nav.endpointCredentials"] = "Thông tin xác thực Agent",
        ["nav.equipment"] = "Thiết bị", ["nav.equipmentRegister"] = "Sổ quản lý thiết bị", ["nav.equipmentImport"] = "Import Excel thiết bị",
        ["nav.data"] = "Dữ liệu", ["nav.departments"] = "Bộ phận", ["nav.employees"] = "Nhân viên", ["nav.approvers"] = "Approver", ["nav.leaveTypes"] = "Hình thức nghỉ",
        ["nav.hrmSync"] = "Đồng bộ HRM", ["nav.hrmConflict"] = "Conflict HRM", ["nav.hrmRoleRules"] = "Role HRM → User", ["nav.approvalPolicy"] = "Approval Policy", ["nav.departmentStatus"] = "Trạng thái phòng ban", ["nav.attendanceCalculation"] = "Tính giờ theo HRM",
        ["nav.otManagement"] = "Quản lý OT", ["nav.otList"] = "Danh sách đơn OT", ["nav.otLimits"] = "Hạn mức OT",
        ["nav.hrReconciliation"] = "Đối soát HR", ["nav.hrQueue"] = "Hàng chờ HR",
        ["nav.payroll"] = "Tính lương", ["nav.payrollTable"] = "Bảng công tính lương",
        ["nav.reports"] = "Báo cáo & thống kê", ["nav.reportCenter"] = "Trung tâm báo cáo", ["nav.reportLeave"] = "Nghỉ phép", ["nav.reportOt"] = "OT", ["nav.reportTrip"] = "Công tác", ["nav.reportEquipment"] = "Thiết bị", ["nav.reportAttendance"] = "Chấm công",
        ["nav.system"] = "Hệ thống", ["nav.admin"] = "Quản trị viên", ["nav.securityCenter"] = "Security Center", ["nav.functionRegistry"] = "Đăng ký chức năng hệ thống", ["nav.emailTemplates"] = "Email mẫu", ["nav.emailQueue"] = "Email chờ gửi", ["nav.workCalendarManagement"] = "Năm & ngày nghỉ",
        ["help.tooltip"] = "Trợ giúp chức năng", ["help.context"] = "Trợ giúp ngữ cảnh", ["help.flow"] = "Luồng thao tác", ["help.steps"] = "Các bước", ["help.example"] = "Ví dụ", ["help.security"] = "Quyền & dữ liệu", ["help.openGuide"] = "Mở tài liệu chi tiết",
        ["home.title"] = "Trang chủ", ["home.workspace"] = "Workspace cá nhân", ["home.greeting"] = "Xin chào", ["home.workspaceDescription"] = "Đây là trung tâm công việc cá nhân: theo dõi đơn, xử lý phê duyệt và các thông tin cần chú ý.", ["home.information"] = "Thông tin", ["home.createLeave"] = "Tạo Leave", ["home.createOt"] = "Tạo OT", ["home.createTrip"] = "Tạo công tác", ["home.workCenter"] = "Trung tâm công việc", ["home.actionCenterDescription"] = "Thông báo và phê duyệt dùng chung một Action Center. Thiết bị cũng được mở ngay trong Workspace.", ["home.approval"] = "Phê duyệt", ["home.approvalDescription"] = "Tập trung các việc đang chờ xử lý theo quyền của tài khoản.", ["home.myLeave"] = "Nghỉ phép của tôi", ["home.myLeaveDescription"] = "Xem số dư, lịch sử và trạng thái các đơn nghỉ.", ["home.myOt"] = "OT của tôi", ["home.myOtDescription"] = "Theo dõi giờ OT và các yêu cầu đã gửi.", ["home.workCalendar"] = "Lịch làm việc", ["home.workCalendarDescription"] = "Xem tổng quan lịch làm việc, chấm công và các đơn trong tháng.", ["home.personalOverview"] = "Tổng quan cá nhân", ["home.updatedAt"] = "Cập nhật {0}", ["home.equipment"] = "Thiết bị", ["home.equipmentDescription"] = "Sổ thiết bị, QR, repair và workspace Excel theo schema phòng ban.", ["home.importExcel"] = "Import Excel", ["home.openEquipment"] = "Mở Equipment", ["home.publicInformation"] = "Thông tin công khai", ["home.publicOnlyDescription"] = "Tài khoản hiện không có capability Dashboard.View. Bạn chỉ thấy các thông tin công khai được phép.", ["home.openPublicInformation"] = "Mở thông tin công khai", ["home.dashboardEmpty"] = "Dashboard chưa có dữ liệu để hiển thị.", ["home.sessionInvalid"] = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", ["home.sessionExpired"] = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", ["home.loadFailed"] = "Không thể tải Dashboard.", ["home.loadError"] = "Không thể tải Dashboard: {0}"
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.profile"] = "マイプロフィール", ["common.changePassword"] = "パスワード変更", ["common.sessions"] = "デバイスとログインセッション",
        ["common.employeeCode"] = "社員番号", ["common.user"] = "FCC社員",
        ["route.forbidden"] = "このページにアクセスする権限がありません。", ["route.notFound"] = "ページが見つかりません（404）。",
        ["nav.workCalendar"] = "勤務カレンダー", ["nav.executionReconciliation"] = "勤務実績照合", ["nav.workInbox"] = "ワークセンター",
        ["nav.public"] = "公開情報", ["nav.publicInformation"] = "公開情報", ["nav.publicForms"] = "申請フォーム", ["nav.publicFormsManage"] = "申請フォーム管理",
        ["nav.accessChange"] = "権限変更", ["nav.accessChangeRequest"] = "権限変更・引継ぎ申請",
        ["nav.approval"] = "承認", ["nav.approvalPending"] = "承認待ち一覧",
        ["nav.leaveOverview"] = "概要", ["nav.leaveCreate"] = "新規申請", ["nav.leaveHistory"] = "休暇履歴",
        ["nav.ot"] = "残業（OT）", ["nav.otOverview"] = "概要", ["nav.otCreate"] = "残業申請", ["nav.otHistory"] = "残業履歴",
        ["nav.tripOverview"] = "概要", ["nav.tripCreate"] = "出張申請", ["nav.tripHistory"] = "出張履歴",
        ["nav.endpointGovernance"] = "Endpoint Governance", ["nav.endpointCatalog"] = "ソフトウェア / サービスカタログ", ["nav.endpointInventory"] = "Endpoint インベントリ", ["nav.endpointCredentials"] = "Agent 認証情報",
        ["nav.equipment"] = "設備", ["nav.equipmentRegister"] = "設備管理台帳", ["nav.equipmentImport"] = "設備Excel取込",
        ["nav.data"] = "データ", ["nav.departments"] = "部門", ["nav.employees"] = "社員", ["nav.approvers"] = "承認者", ["nav.leaveTypes"] = "休暇種別",
        ["nav.hrmSync"] = "HRM同期", ["nav.hrmConflict"] = "HRM競合", ["nav.hrmRoleRules"] = "HRMロール → ユーザー", ["nav.approvalPolicy"] = "承認ポリシー", ["nav.departmentStatus"] = "部門状況", ["nav.attendanceCalculation"] = "HRM勤怠計算",
        ["nav.otManagement"] = "残業管理", ["nav.otList"] = "残業申請一覧", ["nav.otLimits"] = "残業上限",
        ["nav.hrReconciliation"] = "人事照合", ["nav.hrQueue"] = "人事確認待ち",
        ["nav.payroll"] = "給与計算", ["nav.payrollTable"] = "給与計算用勤務表",
        ["nav.reports"] = "レポート・統計", ["nav.reportCenter"] = "レポートセンター", ["nav.reportLeave"] = "休暇", ["nav.reportOt"] = "残業", ["nav.reportTrip"] = "出張", ["nav.reportEquipment"] = "設備", ["nav.reportAttendance"] = "勤怠",
        ["nav.system"] = "システム", ["nav.admin"] = "管理者", ["nav.securityCenter"] = "セキュリティセンター", ["nav.functionRegistry"] = "システム機能登録", ["nav.emailTemplates"] = "メールテンプレート", ["nav.emailQueue"] = "送信待ちメール", ["nav.workCalendarManagement"] = "年度・休日",
        ["help.tooltip"] = "機能ヘルプ", ["help.context"] = "コンテキストヘルプ", ["help.flow"] = "操作フロー", ["help.steps"] = "手順", ["help.example"] = "例", ["help.security"] = "権限とデータ", ["help.openGuide"] = "詳細ガイドを開く",
        ["home.title"] = "ホーム", ["home.workspace"] = "個人ワークスペース", ["home.greeting"] = "こんにちは", ["home.workspaceDescription"] = "申請、承認、重要なお知らせをまとめて確認する個人ワークスペースです。", ["home.information"] = "情報", ["home.createLeave"] = "休暇申請", ["home.createOt"] = "残業申請", ["home.createTrip"] = "出張申請", ["home.workCenter"] = "ワークセンター", ["home.actionCenterDescription"] = "通知と承認を一つのAction Centerに集約しています。設備もWorkspaceから開けます。", ["home.approval"] = "承認", ["home.approvalDescription"] = "アカウントの権限に応じて処理待ちのタスクを確認します。", ["home.myLeave"] = "自分の休暇", ["home.myLeaveDescription"] = "残日数、履歴、休暇申請の状態を確認します。", ["home.myOt"] = "自分の残業", ["home.myOtDescription"] = "残業時間と申請履歴を確認します。", ["home.workCalendar"] = "勤務カレンダー", ["home.workCalendarDescription"] = "月間の勤務予定、勤怠、申請を確認します。", ["home.personalOverview"] = "個人概要", ["home.updatedAt"] = "更新 {0}", ["home.equipment"] = "設備", ["home.equipmentDescription"] = "設備台帳、QR、修理、部門スキーマに基づくExcel Workspaceです。", ["home.importExcel"] = "Excel取込", ["home.openEquipment"] = "設備を開く", ["home.publicInformation"] = "公開情報", ["home.publicOnlyDescription"] = "このアカウントにはDashboard.View権限がありません。許可された公開情報のみ表示します。", ["home.openPublicInformation"] = "公開情報を開く", ["home.dashboardEmpty"] = "表示できるDashboardデータがありません。", ["home.sessionInvalid"] = "ログインセッションが無効または期限切れです。", ["home.sessionExpired"] = "ログインセッションの有効期限が切れました。再度ログインしてください。", ["home.loadFailed"] = "Dashboardを読み込めませんでした。", ["home.loadError"] = "Dashboardを読み込めませんでした: {0}"
    };

    public static bool TryGet(LanguageCode language, string key, out string value) =>
        (language == LanguageCode.Ja ? Ja : Vi).TryGetValue(key, out value!);
}

# FVN_REGISTER01 – i18n audit (branch feature/i18n-vi-ja)

- Catalog: 694 key vi = 694 key ja, 0 key thiếu, 0 key sai module
- .razor: 125 file — OK 38, làm dở 76, chưa làm 11
- Số hit hard-code là ước lượng bằng regex (có thể dư/thiếu vài dòng)

## A. UI .razor (làm theo thứ tự từ trên xuống)

| # | File | Module đích (Localization/{lang}.{module}.json) | T() hiện có | Hard-code còn lại | Trạng thái |
|---|---|---|---|---|---|
| 1 | `FVN_REGISTER.Shared/Pages/EquipmentHelp.razor` | `equipment` | 0 | 35 | CHƯA LÀM |
| 2 | `FVN_REGISTER.Shared/Pages/EndpointCredentials.razor` | `endpoint` | 2 | 0 | OK |
| 3 | `FVN_REGISTER.Shared/Pages/EquipmentRepair.razor` | `equipment` | 0 | 11 | CHƯA LÀM |
| 4 | `FVN_REGISTER.Web/Components/Pages/Error.razor` | `common` | 0 | 6 | ĐẶC BIỆT — SSR/error scaffold |
| 5 | `FVN_REGISTER.Shared/Pages/Reports/ReportBase.razor` | `report` | 0 | 5 | CHƯA LÀM |
| 6 | `FVN_REGISTER.Shared/Dialogs/Approvals/RejectDialog.razor` | `approval` | 0 | 4 | CHƯA LÀM |
| 7 | `FVN_REGISTER.Shared/Pages/NotificationBell.razor` | `common` | 0 | 4 | CHƯA LÀM |
| 8 | `FVN_REGISTER.Shared/Pages/LeaveOverview.razor` | `leave` | 2 | 0 | OK |
| 9 | `FVN_REGISTER.Shared/Pages/OT/OTOverview.razor` | `ot` | 2 | 0 | OK |
| 10 | `FVN_REGISTER.Shared/Pages/Public.razor` | `public` | 5 | 0 | OK |
| 11 | `FVN_REGISTER.Shared/Pages/TripOverview.razor` | `trip` | 2 | 0 | OK |
| 12 | `FVN_REGISTER.Shared/Pages/EquipmentSchemaManagement.razor` | `equipment` | 5 | 61 | LÀM DỞ |
| 13 | `FVN_REGISTER.Shared/Pages/EquipmentInspectionManagement.razor` | `equipment` | 14 | 57 | LÀM DỞ |
| 14 | `FVN_REGISTER.Shared/Pages/Emails/EmailTemplates.razor` | `common` | 5 | 56 | LÀM DỞ |
| 15 | `FVN_REGISTER.Shared/Pages/EndpointGovernance.razor` | `endpoint` | 6 | 39 | LÀM DỞ |
| 16 | `FVN_REGISTER.Shared/Pages/Equipment.razor` | `equipment` | 14 | 39 | LÀM DỞ |
| 17 | `FVN_REGISTER.Shared/Pages/Admin/SecurityCenter.razor` | `security` | 26 | 36 | LÀM DỞ |
| 18 | `FVN_REGISTER.Shared/Pages/Admin/SecurityFunctionRegistry.razor` | `security` | 3 | 30 | LÀM DỞ |
| 19 | `FVN_REGISTER.Shared/Pages/EquipmentScan.razor` | `equipment` | 8 | 27 | LÀM DỞ |
| 20 | `FVN_REGISTER.Shared/Pages/Execution/ExecutionReconciliation.razor` | `execution` | 12 | 24 | LÀM DỞ |
| 21 | `FVN_REGISTER.Shared/Pages/EquipmentWorkspace.razor` | `equipment` | 2 | 23 | LÀM DỞ |
| 22 | `FVN_REGISTER.Shared/Pages/Admin/ApprovalPolicies.razor` | `approval` | 6 | 22 | LÀM DỞ |
| 23 | `FVN_REGISTER.Shared/Dialogs/LeaveAddDialog.razor` | `leave` | 3 | 21 | LÀM DỞ |
| 24 | `FVN_REGISTER.Shared/Pages/Admin/WorkCalendarManagement.razor` | `calendar` | 14 | 21 | LÀM DỞ |
| 25 | `FVN_REGISTER.Shared/Pages/Departments/DepartmentStatus.razor` | `common` | 9 | 21 | LÀM DỞ |
| 26 | `FVN_REGISTER.Shared/Pages/LeaveTypes/LeaveTypeManagement.razor` | `leave` | 5 | 21 | LÀM DỞ |
| 27 | `FVN_REGISTER.Shared/Dialogs/Employees/EmployeeEditDialog.razor` | `common` | 2 | 20 | LÀM DỞ |
| 28 | `FVN_REGISTER.Shared/Pages/Admin/PublicInformation.razor` | `public` | 4 | 20 | LÀM DỞ |
| 29 | `FVN_REGISTER.Shared/Pages/EndpointInventory.razor` | `endpoint` | 1 | 20 | LÀM DỞ |
| 30 | `FVN_REGISTER.Shared/Pages/TripCreate.razor` | `trip` | 3 | 20 | LÀM DỞ |
| 31 | `FVN_REGISTER.Shared/Pages/Admin/PublicForms.razor` | `public` | 14 | 18 | LÀM DỞ |
| 32 | `FVN_REGISTER.Shared/Pages/EquipmentFormManagement.razor` | `equipment` | 6 | 18 | LÀM DỞ |
| 33 | `FVN_REGISTER.Shared/Pages/Approvals/ApproverManagement.razor` | `approval` | 8 | 17 | LÀM DỞ |
| 34 | `FVN_REGISTER.Shared/Dialogs/OT/OTLimitRuleEditDialog.razor` | `ot` | 4 | 16 | LÀM DỞ |
| 35 | `FVN_REGISTER.Shared/Pages/Employees/EmployeeManagement.razor` | `common` | 7 | 16 | LÀM DỞ |
| 36 | `FVN_REGISTER.Shared/Pages/OT/OTCreate.razor` | `ot` | 7 | 16 | LÀM DỞ |
| 37 | `FVN_REGISTER.Shared/Dialogs/Approvals/ApproverEditDialog.razor` | `approval` | 6 | 14 | LÀM DỞ |
| 38 | `FVN_REGISTER.Shared/Pages/Approvals/Approval.razor` | `approval` | 10 | 14 | LÀM DỞ |
| 39 | `FVN_REGISTER.Shared/Components/OT/OTDetailDialog.razor` | `ot` | 8 | 13 | LÀM DỞ |
| 40 | `FVN_REGISTER.Shared/Pages/Histories/HistoryPage.razor` | `common` | 12 | 13 | LÀM DỞ |
| 41 | `FVN_REGISTER.Shared/Pages/AccessChange.razor` | `access` | 35 | 12 | LÀM DỞ |
| 42 | `FVN_REGISTER.Shared/Pages/OT/OTDetailPage.razor` | `ot` | 5 | 12 | LÀM DỞ |
| 43 | `FVN_REGISTER.Shared/Pages/Reports/ReportCenter.razor` | `report` | 4 | 12 | LÀM DỞ |
| 44 | `FVN_REGISTER.Shared/Dialogs/OT/OTDetailDialog.razor` | `ot` | 4 | 11 | LÀM DỞ |
| 45 | `FVN_REGISTER.Shared/Pages/Departments/DepartmentManagement.razor` | `common` | 6 | 11 | LÀM DỞ |
| 46 | `FVN_REGISTER.Shared/Pages/Execution/ExecutionHrReview.razor` | `execution` | 9 | 11 | LÀM DỞ |
| 47 | `FVN_REGISTER.Shared/Pages/OT/OTLimitRuleManagement.razor` | `ot` | 12 | 11 | LÀM DỞ |
| 48 | `FVN_REGISTER.Shared/Components/LeaveTypes/LeaveTypeEditDialog.razor` | `leave` | 1 | 10 | LÀM DỞ |
| 49 | `FVN_REGISTER.Shared/Dialogs/LeaveDetailDialog.razor` | `leave` | 3 | 10 | LÀM DỞ |
| 50 | `FVN_REGISTER.Shared/Pages/Admin/EmailQueueManager.razor` | `common` | 6 | 10 | LÀM DỞ |
| 51 | `FVN_REGISTER.Shared/Pages/EquipmentInspection.razor` | `equipment` | 8 | 10 | LÀM DỞ |
| 52 | `FVN_REGISTER.Shared/Pages/TripDetail.razor` | `trip` | 7 | 10 | LÀM DỞ |
| 53 | `FVN_REGISTER.Shared/Pages/UserManagers/Managers.razor` | `common` | 8 | 10 | LÀM DỞ |
| 54 | `FVN_REGISTER.Shared/Pages/Admin/HrmSyncReview.razor` | `hrm` | 2 | 9 | LÀM DỞ |
| 55 | `FVN_REGISTER.Shared/Pages/Reports/OperationalReport.razor` | `report` | 8 | 9 | LÀM DỞ |
| 56 | `FVN_REGISTER.Shared/Pages/WorkInbox.razor` | `workspace` | 2 | 9 | LÀM DỞ |
| 57 | `FVN_REGISTER.Shared/Dialogs/Approvals/ApproveDetailDialog.razor` | `approval` | 3 | 8 | LÀM DỞ |
| 58 | `FVN_REGISTER.Shared/Pages/Admin/HrmSync.razor` | `hrm` | 3 | 8 | LÀM DỞ |
| 59 | `FVN_REGISTER.Shared/Pages/Payroll/PayrollTable.razor` | `payroll` | 4 | 8 | LÀM DỞ |
| 60 | `FVN_REGISTER.Shared/Pages/Admin/PublicFormSubmissions.razor` | `public` | 9 | 7 | LÀM DỞ |
| 61 | `FVN_REGISTER.Shared/Pages/ChangePassword.razor` | `login` | 1 | 7 | LÀM DỞ |
| 62 | `FVN_REGISTER.Shared/Pages/EquipmentRequestForms.razor` | `equipment` | 1 | 7 | LÀM DỞ |
| 63 | `FVN_REGISTER.Shared/Pages/FeatureGuide.razor` | `common` | 1 | 7 | LÀM DỞ |
| 64 | `FVN_REGISTER.Shared/Pages/HrmAttendanceCalculation.razor` | `hrm` | 4 | 7 | LÀM DỞ |
| 65 | `FVN_REGISTER.Shared/Pages/OT/OTLISTs.razor` | `ot` | 10 | 7 | LÀM DỞ |
| 66 | `FVN_REGISTER.Shared/Pages/PublicFormDetail.razor` | `public` | 3 | 7 | LÀM DỞ |
| 67 | `FVN_REGISTER.Shared/Pages/Reports/LeaveReport.razor` | `leave` | 3 | 7 | LÀM DỞ |
| 68 | `FVN_REGISTER.Shared/Pages/Reports/OTReport.razor` | `report` | 4 | 7 | LÀM DỞ |
| 69 | `FVN_REGISTER.Shared/Pages/TripHistory.razor` | `trip` | 2 | 7 | LÀM DỞ |
| 70 | `FVN_REGISTER.Shared/Pages/Admin/HrmRoleRules.razor` | `hrm` | 9 | 6 | LÀM DỞ |
| 71 | `FVN_REGISTER.Shared/Pages/EquipmentReports.razor` | `equipment` | 9 | 6 | LÀM DỞ |
| 72 | `FVN_REGISTER.Shared/Components/LeaveTypes/LeaveTypeDetailDialog.razor` | `leave` | 4 | 5 | LÀM DỞ |
| 73 | `FVN_REGISTER.Shared/Components/Security/EffectivePermissionPreview.razor` | `security` | 3 | 5 | LÀM DỞ |
| 74 | `FVN_REGISTER.Shared/Components/Security/ManagedScopeEditor.razor` | `security` | 3 | 5 | LÀM DỞ |
| 75 | `FVN_REGISTER.Shared/Pages/Admin/SessionManagement.razor` | `security` | 9 | 5 | LÀM DỞ |
| 76 | `FVN_REGISTER.Shared/Pages/PublicInfo.razor` | `public` | 2 | 5 | LÀM DỞ |
| 77 | `FVN_REGISTER.Shared/Pages/UserProfile.razor` | `common` | 2 | 5 | LÀM DỞ |
| 78 | `FVN_REGISTER.Shared/Dialogs/ApproveDetailDialog.razor` | `approval` | 5 | 4 | LÀM DỞ |
| 79 | `FVN_REGISTER.Shared/Dialogs/Leaves/RequestDetailDialog.razor` | `leave` | 1 | 4 | LÀM DỞ |
| 80 | `FVN_REGISTER.Shared/Dialogs/Users/CreateUserDialog.razor` | `common` | 5 | 4 | LÀM DỞ |
| 81 | `FVN_REGISTER.Shared/Dialogs/Departments/DepartmentManagementEditDialog.razor` | `common` | 1 | 3 | LÀM DỞ |
| 82 | `FVN_REGISTER.Shared/Dialogs/EmployeePickerDialog.razor` | `common` | 2 | 3 | LÀM DỞ |
| 83 | `FVN_REGISTER.Shared/Dialogs/Employees/EmployeeOTDetailDialog.razor` | `ot` | 3 | 3 | LÀM DỞ |
| 84 | `FVN_REGISTER.Shared/Dialogs/TodayOTDialog.razor` | `ot` | 1 | 3 | LÀM DỞ |
| 85 | `FVN_REGISTER.Shared/Pages/NotificationBadge.razor` | `common` | 1 | 3 | LÀM DỞ |
| 86 | `FVN_REGISTER.Shared/Pages/PublicForms.razor` | `public` | 1 | 3 | LÀM DỞ |
| 87 | `FVN_REGISTER.Shared/Dialogs/Users/EditUserDialog.razor` | `common` | 5 | 2 | LÀM DỞ |
| 88 | `FVN_REGISTER.Shared/Components/EquipmentChecklistTree.razor` | `equipment` | 0 | 1 | OK |
| 89 | `FVN_REGISTER.Shared/Components/OT/OTApprovalList.razor` | `approval` | 1 | 1 | OK |
| 90 | `FVN_REGISTER.Shared/Dialogs/ApproveConfirmDialog.razor` | `approval` | 1 | 1 | OK |
| 91 | `FVN_REGISTER.Shared/Dialogs/ConfirmDialog.razor` | `common` | 0 | 1 | OK |
| 92 | `FVN_REGISTER.Shared/Dialogs/Users/ResetPasswordDialog.razor` | `common` | 1 | 1 | OK |
| 93 | `FVN_REGISTER.Shared/Layout/MainLayout.razor` | `common` | 10 | 1 | OK |
| 94 | `FVN_REGISTER.Shared/Pages/EquipmentHandover.razor` | `equipment` | 1 | 1 | OK |
| 95 | `FVN_REGISTER.Shared/Pages/Home.razor` | `home` | 32 | 1 | OK |
| 96 | `FVN_REGISTER.Shared/Pages/Login.razor` | `login` | 34 | 1 | OK |
| 97 | `FVN_REGISTER.Shared/Pages/OT/OTApprovals.razor` | `approval` | 1 | 1 | OK |
| 98 | `FVN_REGISTER.Shared/Pages/WorkCalendarPage.razor` | `calendar` | 19 | 1 | OK |
| 99 | `FVN_REGISTER.Shared/Components/ApprovalRouteSelector.razor` | `approval` | 5 | 0 | OK |
| 100 | `FVN_REGISTER.Shared/Components/Calendar/CalendarAttendanceFeedbackDialog.razor` | `calendar` | 34 | 0 | OK |
| 101 | `FVN_REGISTER.Shared/Components/Calendar/CalendarDayDialog.razor` | `calendar` | 22 | 0 | OK |
| 102 | `FVN_REGISTER.Shared/Components/Calendar/CalendarRegistrationDialog.razor` | `calendar` | 7 | 0 | OK |
| 103 | `FVN_REGISTER.Shared/Components/Calendar/WorkCalendar.razor` | `calendar` | 20 | 0 | OK |
| 104 | `FVN_REGISTER.Shared/Components/DashboardOverview.razor` | `dashboard` | 42 | 0 | OK |
| 105 | `FVN_REGISTER.Shared/Components/EquipmentDashboardTile.razor` | `equipment` | 10 | 0 | OK |
| 106 | `FVN_REGISTER.Shared/Components/EquipmentQrScanner.razor` | `equipment` | 8 | 0 | OK |
| 107 | `FVN_REGISTER.Shared/Components/FeatureHelp.razor` | `common` | 7 | 0 | OK |
| 108 | `FVN_REGISTER.Shared/Components/Leaves/LeaveListSection.razor` | `leave` | 9 | 0 | OK |
| 109 | `FVN_REGISTER.Shared/Components/ModuleOverview.razor` | `common` | 7 | 0 | OK |
| 110 | `FVN_REGISTER.Shared/Components/OT/OTApprovalProgressBar.razor` | `approval` | 0 | 0 | OK |
| 111 | `FVN_REGISTER.Shared/Components/RouteStatusPopup.razor` | `common` | 0 | 0 | OK |
| 112 | `FVN_REGISTER.Shared/Dialogs/Departments/DepartmentManagementDeleteConfirmDialog.razor` | `common` | 2 | 0 | OK |
| 113 | `FVN_REGISTER.Shared/Layout/EmptyLayout.razor` | `common` | 0 | 0 | OK |
| 114 | `FVN_REGISTER.Shared/Layout/NavMenu.razor` | `nav` | 65 | 0 | OK |
| 115 | `FVN_REGISTER.Shared/Pages/LeaveCreate.razor` | `leave` | 7 | 0 | OK |
| 116 | `FVN_REGISTER.Shared/Pages/LeaveHistory.razor` | `leave` | 1 | 0 | OK |
| 117 | `FVN_REGISTER.Shared/Pages/OT/OTHistory.razor` | `ot` | 1 | 0 | OK |
| 118 | `FVN_REGISTER.Shared/Pages/RedirectToLogin.razor` | `login` | 0 | 0 | OK |
| 119 | `FVN_REGISTER.Shared/Routes.razor` | `common` | 3 | 0 | OK |
| 120 | `FVN_REGISTER.Shared/_Imports.razor` | `common` | 0 | 0 | OK |
| 121 | `FVN_REGISTER.UI/Components/_Imports.razor` | `common` | 0 | 0 | OK |
| 122 | `FVN_REGISTER.Web/Components/App.razor` | `common` | 0 | 0 | OK |
| 123 | `FVN_REGISTER.Web/Components/PwaInstallPrompt.razor` | `common` | 0 | 0 | OK |
| 124 | `FVN_REGISTER.Web/Components/Security/SecurityManifestPublisher.razor` | `security` | 0 | 0 | OK |
| 125 | `FVN_REGISTER.Web/Components/_Imports.razor` | `common` | 0 | 0 | OK |

## B. File .cs còn chuỗi tiếng Việt (Shared + Web)

| File | Số dòng |
|---|---|
| `FVN_REGISTER.Web/Security/SecurityDiscoveryManifest.cs` | 24 |
| `FVN_REGISTER.Shared/Services/Users/AuthClientService.cs` | 11 |
| `FVN_REGISTER.Shared/Services/Equipment/EquipmentClientService.Schema.cs` | 7 |
| `FVN_REGISTER.Shared/Services/Trips/TripClientService.cs` | 6 |
| `FVN_REGISTER.Shared/Services/Equipment/EquipmentClientService.cs` | 5 |
| `FVN_REGISTER.Shared/Services/Leaves/LeaveTypeClientService.cs` | 5 |
| `FVN_REGISTER.Shared/Pages/LeaveCreate.razor.cs` | 4 |
| `FVN_REGISTER.Shared/Handlers/AuthorizedHttpClient.cs` | 4 |
| `FVN_REGISTER.Shared/Services/Security/EndpointGovernanceClientService.cs` | 4 |
| `FVN_REGISTER.Shared/Services/Leaves/LeaveCreateClientService.cs` | 4 |
| `FVN_REGISTER.Shared/Services/Security/EndpointCredentialClientService.cs` | 3 |
| `FVN_REGISTER.Shared/Services/Security/EndpointInventoryClientService.cs` | 3 |
| `FVN_REGISTER.Shared/Services/OTs/DeptClientService.cs` | 3 |
| `FVN_REGISTER.Shared/Utils/LeaveTypeUIExtensions.cs` | 2 |
| `FVN_REGISTER.Shared/Utils/Extentions/LeaveRequestDtoUIExtensions.cs` | 2 |
| `FVN_REGISTER.Shared/Services/Security/AccessChangeClientService.cs` | 2 |
| `FVN_REGISTER.Shared/Services/Departments/DepartmentStatusClientService.cs` | 2 |
| `FVN_REGISTER.Shared/Utils/Extentions/EmployeeCardUIExtensions.cs` | 1 |
| `FVN_REGISTER.Shared/Utils/Extentions/OTMonthlyUIExtensions.cs` | 1 |
| `FVN_REGISTER.Shared/Utils/Helpers/BoolUIExtensions.cs` | 1 |
| `FVN_REGISTER.Shared/Services/Dashboards/DashboardClientService.cs` | 1 |
| `FVN_REGISTER.Shared/Services/Leaves/LeaveHistorysClientService.cs` | 1 |

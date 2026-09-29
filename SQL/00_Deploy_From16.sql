/*
===============================================================================
FVN_REGISTER deployment from SQL/16 onward.
===============================================================================
SSMS: enable Query -> SQLCMD Mode before executing this file.
Current SQL directory:
  H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL
===============================================================================
*/
:on error exit
:setvar RepoRoot "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL"
:r "16_EquipmentFlexibleImport.sql"
:r "16A_EquipmentCapabilities.sql"
:r "17_ApprovalRouteSelection.sql"
:r "18_ApproverConfigurationReview.sql"
:r "19_OT_LimitRule_ScopeColumns.sql"
:r "20_Reports.sql"
:r "21_Verify_Reports.sql"
:r "22_00_HrmAttendanceTables.sql"
:r "22_02_HrmCompatibleTimeKeepingForStaff.sql"
:r "22_03_CalculateHrmAttendance.sql"
:r "22_03A_DepartmentCodeCompatibility.sql"
:r "22_04_HrmAttendanceHistory.sql"
:r "22_05_HrmAttendanceBatchSnapshot.sql"
:r "23_LeaveBalanceUpgrade.sql"
:r "24_WorkYearUpgrade.sql"
:r "25_RemoveLegacyOTSync.sql"
:r "26_WorkCalendarAction.sql"
:r "27_WorkCalendarActionIndexesSeed.sql"
:r "28_Verify_WorkCalendarAction.sql"
:r "28B_Trips.sql"
:r "29_ExecutionReconciliation.sql"
:r "30_Payroll.sql"
:r "32_ExecutionReviewSecurity.sql"
:r "32_PasswordResetRequests.sql"
:r "33_DocumentationConsistency.sql"
:r "34_OT_Leave_Limits.sql"
:r "35_WorkCalendar.sql"
:r "36_ApprovalPolicyDepartmentPosition.sql"
:r "31_Hrm_User_Approval_Provisioning.sql"
:r "37_PublicRegistrationForms.sql"
:r "16B_EquipmentSpecificRequestForms.sql"
:r "39_AuditBaseCompatibility.sql"
:r "40_EmailCenter.sql"
:r "41_EmailCenter_ProfileCompatibility.sql"
:r "41_SecurityFunctionCleanup.sql"
:r "42_EquipmentInspection.sql"
:r "43_EquipmentHandover.sql"
:r "44_SecurityAccessChange.sql"
:r "45_EquipmentResponsibilityAndRepair.sql"
:r "46_SecurityFunctionRegistry.sql"
:r "47_Verify_SecurityFunctionRegistry.sql"
:r "48_SecurityTwoFactorSuperAdminCompatibility.sql"
:r "49_Verify_SecurityTwoFactorSuperAdmin.sql"
:r "50_EquipmentManageCapability.sql"
:r "51_SecurityFunctionRegistryRecovery.sql"
:r "52_Verify_SecurityFunctionRecovery.sql"
:r "53_ApprovalUnifiedFoundation.sql
"

/* The 01-15 baseline normally contains the canonical RBAC catalog.  Keep this
   patch here as well so From16 can repair the capability on an existing DB. */
:r "14E_AttendanceCalculateCapability.sql"

/* ENDPOINT GOVERNANCE - schema must exist before capability seed/verify */
:r "54_Endpoint_Inventory_Compliance.sql"
:r "54_EndpointGovernanceFoundation.sql"
:r "55_Endpoint_Credentials.sql"
:r "55_EndpointGovernanceCatalogApproval.sql"
:r "56_EndpointGovernanceExtensions.sql"
:r "57_Verify_Endpoint_Governance.sql"
:r "58_EndpointSecurityCapabilities.sql"
:r "59_CalendarViewCapability.sql"
:r "60_EndpointAgentDataHardening.sql"
:r "61_RequestModuleEmailTemplates.sql"

/* Authorization schema gate for an already-established 01-15 baseline. */
:r "14D_SecuritySchemaVerify.sql"
:r "12_Verify.sql"
:r "99_Verify.sql"

PRINT N'FVN_REGISTER SQL deployment completed.';
GO

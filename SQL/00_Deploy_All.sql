/*
===============================================================================
FVN_REGISTER - MASTER SQL DEPLOYMENT (corrected order)
===============================================================================
HOW TO RUN (recommended): use Deploy.ps1 in this folder. It checks that every
file exists, fixes files that do not end with a newline, and runs sqlcmd with
the required switches (-I -b -f 65001).

MANUAL RUN from PowerShell/cmd (must be inside the SQL folder, because all
paths below are relative to the current directory):
  cd "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL"
  sqlcmd -S "SERVER\INSTANCE" -U sa -I -b -f 65001 -i "00_Deploy_All.sql"

SSMS: relative :r paths depend on the SSMS working directory and are not
reliable. Use Deploy.ps1 or sqlcmd instead.

Database/ is documentation/history only and is not executed by deployment.
===============================================================================
*/

:on error exit

/* CORE DATABASE FOUNDATION */
:r "01_Database.sql"
:r "02A_Preflight.sql"
:r "02B_Schemas.sql"
:r "02C_TablePrerequisites.sql"
:r "03_Tables.sql"
:r "04_Constraints.sql"
:r "05_Indexes.sql"
-- 06_Seed.sql is NOT part of the normal deployment.
:r "07_Views.sql"
:r "08_Functions.sql"
:r "09_StoredProcedures.sql"
:r "10A_Audit.sql"
:r "10B_Triggers.sql"
:r "11A_Automation.sql"
:r "11B_Permissions.sql"

/* HRM / SECURITY / PUBLIC INFORMATION */
:r "13_HrmShiftMaster.sql"
:r "14_SecurityAuthorization.sql"
/* F03Users 2FA schema must exist before any later 2FA/security consumers. */
:r "14A_SecurityTwoFactorColumns.sql"
:r "15_PublicInformation.sql"

/* EQUIPMENT - CANONICAL OWNER */
:r "16_EquipmentFlexibleImport.sql"
:r "16A_EquipmentCapabilities.sql"
-- 16B_EquipmentSpecificRequestForms.sql runs after 37.

/* APPROVAL / OT / REPORTS / ATTENDANCE / LEAVE / WORK CALENDAR */
:r "17_ApprovalRouteSelection.sql"
:r "18_ApproverConfigurationReview.sql"
:r "19_OT_LimitRule_ScopeColumns.sql"
:r "20_Reports.sql"
:r "21_Verify_Reports.sql"
:r "22_00_HrmAttendanceTables.sql"
:r "22_02_HrmCompatibleTimeKeepingForStaff.sql"
:r "22_03_CalculateHrmAttendance.sql"
:r "22_04_HrmAttendanceHistory.sql"
:r "23_LeaveBalanceUpgrade.sql"
:r "24_WorkYearUpgrade.sql"
:r "25_RemoveLegacyOTSync.sql"
:r "26_WorkCalendarAction.sql"
:r "27_WorkCalendarActionIndexesSeed.sql"
:r "28_Verify_WorkCalendarAction.sql"

/* TRIPS */
:r "28B_Trips.sql"

/* EXECUTION / PAYROLL / APPROVAL / PUBLIC / EMAIL / SECURITY */
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

/* EQUIPMENT EXTENSIONS */
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

/* APPROVAL FOUNDATION BEFORE ENDPOINT SECURITY */
:r "53_ApprovalUnifiedFoundation.sql"
:r "58_EndpointSecurityCapabilities.sql"

/* ENDPOINT / GOVERNANCE */
:r "54_Endpoint_Inventory_Compliance.sql"
:r "54_EndpointGovernanceFoundation.sql"
:r "55_Endpoint_Credentials.sql"
:r "55_EndpointGovernanceCatalogApproval.sql"
:r "56_EndpointGovernanceExtensions.sql"
:r "57_Verify_Endpoint_Governance.sql"

/* FINAL VERIFICATION */
:r "12_Verify.sql"
:r "99_Verify.sql"

PRINT N'============================================================';
PRINT N'FVN_REGISTER SQL deployment completed.';
PRINT N'============================================================';
GO

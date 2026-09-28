/*
===============================================================================
FVN_REGISTER - MASTER SQL DEPLOYMENT
===============================================================================
Run this file from SSMS with Query -> SQLCMD Mode enabled.
IMPORTANT: SQLCMD mode is required because this master file uses :r directives.
The file is intended to be opened/executed while the current working directory
is the repository SQL folder. RepoRoot is therefore a simple relative path.

ALL repository database migrations are owned by this SQL deployment chain.
Database/ is documentation/history only and is not executed by deployment.
===============================================================================
*/

:on error exit
:setvar RepoRoot "."

/* ---------------------------------------------------------------------------
   CORE DATABASE FOUNDATION
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\01_Database.sql"
:r "$(RepoRoot)\02A_Preflight.sql"
:r "$(RepoRoot)\02B_Schemas.sql"
:r "$(RepoRoot)\02C_TablePrerequisites.sql"
:r "$(RepoRoot)\03_Tables.sql"
:r "$(RepoRoot)\04_Constraints.sql"
:r "$(RepoRoot)\05_Indexes.sql"

/* 06_Seed.sql is intentionally excluded from production/master deployment. */
:rem 06_Seed.sql intentionally excluded; run only against a disposable/test database.

:r "$(RepoRoot)\07_Views.sql"
:r "$(RepoRoot)\08_Functions.sql"
:r "$(RepoRoot)\09_StoredProcedures.sql"
:r "$(RepoRoot)\10A_Audit.sql"
:r "$(RepoRoot)\10B_Triggers.sql"
:r "$(RepoRoot)\11A_Automation.sql"
:r "$(RepoRoot)\11B_Permissions.sql"
:r "$(RepoRoot)\12_Verify.sql"

/* ---------------------------------------------------------------------------
   HRM / SECURITY / PUBLIC INFORMATION
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\13_HrmShiftMaster.sql"
:r "$(RepoRoot)\14_SecurityAuthorization.sql"
:r "$(RepoRoot)\15_PublicInformation.sql"

/* ---------------------------------------------------------------------------
   EQUIPMENT - CANONICAL OWNER
   16 creates/upgrades the complete Equipment foundation before any extension.
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\16_EquipmentFlexibleImport.sql"
:r "$(RepoRoot)\16A_EquipmentCapabilities.sql"
:r "$(RepoRoot)\16B_EquipmentSpecificRequestForms.sql"

/* ---------------------------------------------------------------------------
   APPROVAL / OT / REPORTS / ATTENDANCE / LEAVE / WORK CALENDAR
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\17_ApprovalRouteSelection.sql"
:r "$(RepoRoot)\18_ApproverConfigurationReview.sql"
:r "$(RepoRoot)\19_OT_LimitRule_ScopeColumns.sql"
:r "$(RepoRoot)\20_Reports.sql"
:r "$(RepoRoot)\21_Verify_Reports.sql"
:r "$(RepoRoot)\22_00_HrmAttendanceTables.sql"
:r "$(RepoRoot)\22_02_HrmCompatibleTimeKeepingForStaff.sql"
:r "$(RepoRoot)\22_03_CalculateHrmAttendance.sql"
:r "$(RepoRoot)\22_04_HrmAttendanceHistory.sql"
:r "$(RepoRoot)\23_LeaveBalanceUpgrade.sql"
:r "$(RepoRoot)\24_WorkYearUpgrade.sql"
:r "$(RepoRoot)\25_RemoveLegacyOTSync.sql"
:r "$(RepoRoot)\26_WorkCalendarAction.sql"
:r "$(RepoRoot)\27_WorkCalendarActionIndexesSeed.sql"
:r "$(RepoRoot)\28_Verify_WorkCalendarAction.sql"

/* ---------------------------------------------------------------------------
   TRIPS - canonical schema migrated from Database/Trips
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\28B_Trips.sql"

/* ---------------------------------------------------------------------------
   EXECUTION / PAYROLL / APPROVAL / PUBLIC / EMAIL / SECURITY
   Keep explicit dependency order; filename numbering is historical and is not
   used as the dependency rule.
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\29_ExecutionReconciliation.sql"
:r "$(RepoRoot)\30_Payroll.sql"
:r "$(RepoRoot)\31_Hrm_User_Approval_Provisioning.sql"
:r "$(RepoRoot)\32_ExecutionReviewSecurity.sql"
:r "$(RepoRoot)\32_PasswordResetRequests.sql"
:r "$(RepoRoot)\33_DocumentationConsistency.sql"
:r "$(RepoRoot)\34_OT_Leave_Limits.sql"
:r "$(RepoRoot)\35_WorkCalendar.sql"
:r "$(RepoRoot)\36_ApprovalPolicyDepartmentPosition.sql"
:r "$(RepoRoot)\37_PublicRegistrationForms.sql"
:r "$(RepoRoot)\39_AuditBaseCompatibility.sql"
:r "$(RepoRoot)\40_EmailCenter.sql"
:r "$(RepoRoot)\41_EmailCenter_ProfileCompatibility.sql"
:r "$(RepoRoot)\41_SecurityFunctionCleanup.sql"

/* ---------------------------------------------------------------------------
   EQUIPMENT EXTENSIONS
   All run after canonical Equipment foundation (16/16A/16B).
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\42_EquipmentInspection.sql"
:r "$(RepoRoot)\43_EquipmentHandover.sql"
:r "$(RepoRoot)\44_SecurityAccessChange.sql"
:r "$(RepoRoot)\45_EquipmentResponsibilityAndRepair.sql"
:r "$(RepoRoot)\46_SecurityFunctionRegistry.sql"
:r "$(RepoRoot)\47_Verify_SecurityFunctionRegistry.sql"
:r "$(RepoRoot)\48_SecurityTwoFactorSuperAdminCompatibility.sql"
:r "$(RepoRoot)\49_Verify_SecurityTwoFactorSuperAdmin.sql"
:r "$(RepoRoot)\50_EquipmentManageCapability.sql"
:r "$(RepoRoot)\51_SecurityFunctionRegistryRecovery.sql"
:r "$(RepoRoot)\52_Verify_SecurityFunctionRecovery.sql"

/* Approval foundation must exist before endpoint governance approval links. */
:r "$(RepoRoot)\53_ApprovalUnifiedFoundation.sql"

/* Endpoint security capability depends on the security foundation above. */
:r "$(RepoRoot)\58_EndpointSecurityCapabilities.sql"

/* ---------------------------------------------------------------------------
   ENDPOINT / GOVERNANCE
   Identity and inventory first, then credentials/catalog/extensions.
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\54_Endpoint_Inventory_Compliance.sql"
:r "$(RepoRoot)\54_EndpointGovernanceFoundation.sql"
:r "$(RepoRoot)\55_Endpoint_Credentials.sql"
:r "$(RepoRoot)\55_EndpointGovernanceCatalogApproval.sql"
:r "$(RepoRoot)\56_EndpointGovernanceExtensions.sql"
:r "$(RepoRoot)\57_Verify_Endpoint_Governance.sql"

/* ---------------------------------------------------------------------------
   FINAL VERIFICATION
   --------------------------------------------------------------------------- */
:r "$(RepoRoot)\99_Verify.sql"

PRINT N'============================================================';
PRINT N'FVN_REGISTER SQL deployment completed.';
PRINT N'============================================================';
GO

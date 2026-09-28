/*
===============================================================================
FVN_REGISTER - MASTER SQL DEPLOYMENT
===============================================================================
SSMS: Query -> SQLCMD Mode must be enabled.

IMPORTANT:
SSMS SQLCMD :r resolves include files from the SQLCMD startup directory,
not reliably from the directory containing this .sql file. This master script
therefore uses explicit absolute paths for every :r include on the deployment
machine.

Current SQL directory:
  H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL

Database/ is documentation/history only and is not executed by deployment.
===============================================================================
*/

:on error exit

/* ---------------------------------------------------------------------------
   CORE DATABASE FOUNDATION
   --------------------------------------------------------------------------- */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\01_Database.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\02A_Preflight.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\02B_Schemas.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\02C_TablePrerequisites.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\03_Tables.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\04_Constraints.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\05_Indexes.sql"

/* Seed is now part of the master deployment. FunctionKey compatibility must
   run immediately before the seed because older databases may have a NOT NULL
   F03Functions.FunctionKey column. */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\06A_FunctionKeyCompatibility.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\06_Seed.sql"

:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\07_Views.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\08_Functions.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\09_StoredProcedures.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\10A_Audit.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\10B_Triggers.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\11A_Automation.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\11B_Permissions.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\12_Verify.sql"

/* HRM / SECURITY / PUBLIC INFORMATION */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\13_HrmShiftMaster.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\14_SecurityAuthorization.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\15_PublicInformation.sql"

/* EQUIPMENT - CANONICAL OWNER */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\16_EquipmentFlexibleImport.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\16A_EquipmentCapabilities.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\16B_EquipmentSpecificRequestForms.sql"

/* APPROVAL / OT / REPORTS / ATTENDANCE / LEAVE / WORK CALENDAR */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\17_ApprovalRouteSelection.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\18_ApproverConfigurationReview.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\19_OT_LimitRule_ScopeColumns.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\20_Reports.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\21_Verify_Reports.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\22_00_HrmAttendanceTables.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\22_02_HrmCompatibleTimeKeepingForStaff.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\22_03_CalculateHrmAttendance.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\22_04_HrmAttendanceHistory.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\23_LeaveBalanceUpgrade.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\24_WorkYearUpgrade.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\25_RemoveLegacyOTSync.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\26_WorkCalendarAction.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\27_WorkCalendarActionIndexesSeed.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\28_Verify_WorkCalendarAction.sql"

/* TRIPS */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\28B_Trips.sql"

/* EXECUTION / PAYROLL / APPROVAL / PUBLIC / EMAIL / SECURITY */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\29_ExecutionReconciliation.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\30_Payroll.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\31_Hrm_User_Approval_Provisioning.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\32_ExecutionReviewSecurity.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\32_PasswordResetRequests.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\33_DocumentationConsistency.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\34_OT_Leave_Limits.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\35_WorkCalendar.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\36_ApprovalPolicyDepartmentPosition.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\37_PublicRegistrationForms.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\39_AuditBaseCompatibility.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\40_EmailCenter.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\41_EmailCenter_ProfileCompatibility.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\41_SecurityFunctionCleanup.sql"

/* EQUIPMENT EXTENSIONS */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\42_EquipmentInspection.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\43_EquipmentHandover.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\44_SecurityAccessChange.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\45_EquipmentResponsibilityAndRepair.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\46_SecurityFunctionRegistry.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\47_Verify_SecurityFunctionRegistry.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\48_SecurityTwoFactorSuperAdminCompatibility.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\49_Verify_SecurityTwoFactorSuperAdmin.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\50_EquipmentManageCapability.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\51_SecurityFunctionRegistryRecovery.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\52_Verify_SecurityFunctionRecovery.sql"

/* APPROVAL FOUNDATION BEFORE ENDPOINT SECURITY */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\53_ApprovalUnifiedFoundation.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\58_EndpointSecurityCapabilities.sql"

/* ENDPOINT / GOVERNANCE */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\54_Endpoint_Inventory_Compliance.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\54_EndpointGovernanceFoundation.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\55_Endpoint_Credentials.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\55_EndpointGovernanceCatalogApproval.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\56_EndpointGovernanceExtensions.sql"
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\57_Verify_Endpoint_Governance.sql"

/* FINAL VERIFICATION */
:r "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL\99_Verify.sql"

PRINT N'============================================================';
PRINT N'FVN_REGISTER SQL deployment completed.';
PRINT N'============================================================';
GO
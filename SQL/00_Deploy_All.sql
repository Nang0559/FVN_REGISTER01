/*
===============================================================================
FVN_REGISTER - MASTER SQL DEPLOYMENT
===============================================================================
Run from the repository SQL directory with SSMS Query -> SQLCMD Mode enabled.
ALL repository database migrations are owned by this SQL deployment chain.
Database/ is documentation/history only and is not executed by deployment.
===============================================================================
*/

:setvar RepoRoot "."
:on error exit

:r "$(RepoRoot)\01_Database.sql"
:r "$(RepoRoot)\02A_Preflight.sql"
:r "$(RepoRoot)\02B_Schemas.sql"
:r "$(RepoRoot)\02C_TablePrerequisites.sql"
:r "$(RepoRoot)\03_Tables.sql"
:r "$(RepoRoot)\04_Constraints.sql"
:r "$(RepoRoot)\05_Indexes.sql"
:rem 06_Seed.sql intentionally excluded; run only against a disposable/test database.
:r "$(RepoRoot)\07_Views.sql"
:r "$(RepoRoot)\08_Functions.sql"
:r "$(RepoRoot)\09_StoredProcedures.sql"
:r "$(RepoRoot)\10A_Audit.sql"
:r "$(RepoRoot)\10B_Triggers.sql"
:r "$(RepoRoot)\11A_Automation.sql"
:r "$(RepoRoot)\11B_Permissions.sql"
:r "$(RepoRoot)\12_Verify.sql"
:r "$(RepoRoot)\13_HrmShiftMaster.sql"
:r "$(RepoRoot)\14_SecurityAuthorization.sql"
:r "$(RepoRoot)\15_PublicInformation.sql"

/* Equipment canonical base + capability + form schema. */
:r "$(RepoRoot)\16_EquipmentFlexibleImport.sql"
:r "$(RepoRoot)\16A_EquipmentCapabilities.sql"
:r "$(RepoRoot)\16B_EquipmentSpecificRequestForms.sql"

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

/* Trips canonical schema migrated from Database/Trips. */
:r "$(RepoRoot)\28B_Trips.sql"

:r "$(RepoRoot)\29_ExecutionReconciliation.sql"
:r "$(RepoRoot)\30_Payroll.sql"
:r "$(RepoRoot)\34_OT_Leave_Limits.sql"
:r "$(RepoRoot)\35_WorkCalendar.sql"
:r "$(RepoRoot)\36_ApprovalPolicyDepartmentPosition.sql"
:r "$(RepoRoot)\31_Hrm_User_Approval_Provisioning.sql"
:r "$(RepoRoot)\32_ExecutionReviewSecurity.sql"
:r "$(RepoRoot)\32_PasswordResetRequests.sql"
:r "$(RepoRoot)\33_DocumentationConsistency.sql"
:r "$(RepoRoot)\37_PublicRegistrationForms.sql"
:r "$(RepoRoot)\39_AuditBaseCompatibility.sql"
:r "$(RepoRoot)\40_EmailCenter.sql"
:r "$(RepoRoot)\41_EmailCenter_ProfileCompatibility.sql"
:r "$(RepoRoot)\41_SecurityFunctionCleanup.sql"

/* Equipment extensions - all execute after canonical Equipment base (16). */
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
:r "$(RepoRoot)\58_EndpointSecurityCapabilities.sql"
:r "$(RepoRoot)\53_ApprovalUnifiedFoundation.sql"

/* Endpoint: agent identity/inventory must exist before governance FK/approval tables. */
:r "$(RepoRoot)\54_Endpoint_Inventory_Compliance.sql"
:r "$(RepoRoot)\54_EndpointGovernanceFoundation.sql"
:r "$(RepoRoot)\55_Endpoint_Credentials.sql"
:r "$(RepoRoot)\55_EndpointGovernanceCatalogApproval.sql"
:r "$(RepoRoot)\56_EndpointGovernanceExtensions.sql"
:r "$(RepoRoot)\57_Verify_Endpoint_Governance.sql"

:r "$(RepoRoot)\99_Verify.sql"

PRINT N'============================================================';
PRINT N'FVN_REGISTER SQL deployment completed.';
PRINT N'============================================================';
GO

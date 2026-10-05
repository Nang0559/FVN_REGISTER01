/* FVN_REGISTER deployment from SQL/48 onward. */
:on error exit
:setvar RepoRoot "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL"
/* DepartmentCode = INT (HRM BPMa) must be in place before any script compares DeptCode with numeric codes. */
:r "05A_DepartmentCodeInt.sql"
:r "48_SecurityTwoFactorSuperAdminCompatibility.sql"
:r "49_Verify_SecurityTwoFactorSuperAdmin.sql"
:r "50_EquipmentManageCapability.sql"
:r "51_SecurityFunctionRegistryRecovery.sql"
:r "52_Verify_SecurityFunctionRecovery.sql"
:r "51_SecurityAdminCompatibility.sql"
:r "53_ApprovalUnifiedFoundation.sql"

/* Repair canonical attendance calculation capability on existing databases. */
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
:r "62_SuperAdminFullAccess.sql"
:r "62_RuntimeSchemaCompatibility.sql"
:r "63_CalendarAttendancePerformance.sql"
:r "66_CalendarHolidayClassification.sql"
:r "67_AttendanceSymbolRules.sql"
:r "55_ExecutionResolutionPolicy.sql"
:r "68_EndpointEquipmentAgentIntegration.sql"
:r "69_Endpoint_LanscopeDeployment.sql"
:r "71_DepartmentCodeInt.sql"

/* SECURITY ROLE/FUNCTION MATRIX */
:r "64_SecurityRoleMatrixCapability.sql"
:r "65_SecuritySystemCriticalInvariant.sql"
:r "14D_SecuritySchemaVerify.sql"
:r "12_Verify.sql"
:r "99_Verify.sql"

PRINT N'FVN_REGISTER SQL deployment completed.';
GO

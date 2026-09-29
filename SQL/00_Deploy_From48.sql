/* FVN_REGISTER deployment from SQL/48 onward. */
:on error exit
:setvar RepoRoot "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL"
:r "48_SecurityTwoFactorSuperAdminCompatibility.sql"
:r "49_Verify_SecurityTwoFactorSuperAdmin.sql"
:r "50_EquipmentManageCapability.sql"
:r "51_SecurityFunctionRegistryRecovery.sql"
:r "52_Verify_SecurityFunctionRecovery.sql"
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
:r "14D_SecuritySchemaVerify.sql"
:r "12_Verify.sql"
:r "99_Verify.sql"

PRINT N'FVN_REGISTER SQL deployment completed.';
GO

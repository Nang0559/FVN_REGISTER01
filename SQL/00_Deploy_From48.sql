/*
===============================================================================
FVN_REGISTER - MASTER SQL DEPLOYMENT
===============================================================================
SSMS: enable Query -> SQLCMD Mode before executing this file.

IMPORTANT:
SQLCMD :r resolves relative paths from the SQLCMD working directory, which is
not reliably the folder containing this file in SSMS. Therefore RepoRoot is
set to the actual local SQL directory used by this deployment machine.

Current SQL directory:
  H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL

Database/ is documentation/history only and is not executed by deployment.
===============================================================================
*/

:on error exit

:setvar RepoRoot "H:\95 - Project\19. FVN_RESITER\FVN_REGISTER_907\SQL"

/* CORE DATABASE FOUNDATION */
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



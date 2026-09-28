/* Canonical Endpoint Governance verification. Legacy SQL/56_Endpoint_Governance.sql is intentionally excluded. */
SET NOCOUNT ON;

DECLARE @required TABLE (TableName sysname NOT NULL);
INSERT INTO @required(TableName) VALUES
(N'F03EndpointDevices'),
(N'F03EndpointSoftwareInventory'),
(N'F03EndpointServiceInventory'),
(N'F03EndpointComplianceFindings'),
(N'F03EndpointGovernancePolicies'),
(N'F03EndpointGovernancePolicyItems'),
(N'F03EndpointGovernanceRequests'),
(N'F03EndpointCredentials');

IF EXISTS
(
    SELECT 1
    FROM @required r
    WHERE OBJECT_ID(N'dbo.' + r.TableName, N'U') IS NULL
)
BEGIN
    SELECT r.TableName AS MissingTable
    FROM @required r
    WHERE OBJECT_ID(N'dbo.' + r.TableName, N'U') IS NULL;
    THROW 51057, 'Endpoint Governance schema is incomplete.', 1;
END;

IF COL_LENGTH(N'dbo.F03EndpointDevices', N'EquipmentAssetId') IS NULL
    THROW 51058, 'F03EndpointDevices.EquipmentAssetId is missing; Equipment linkage is required.', 1;

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'IsPublished') IS NULL
    THROW 51059, 'F03EndpointGovernancePolicies.IsPublished is missing.', 1;

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicyItems', N'NormalizedName') IS NULL
    THROW 51060, 'F03EndpointGovernancePolicyItems.NormalizedName is missing.', 1;

SELECT N'Endpoint Governance schema OK' AS Result;
GO

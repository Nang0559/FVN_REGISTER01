/*
===============================================================================
FVN_REGISTER - COMPLETE TEST / DEMO SEED PACK
===============================================================================
This file is intentionally NOT part of normal production deployment.
Deploy.ps1 executes it only when -IncludeSeed is supplied.

Design:
- Reuse the canonical 06_Seed.sql for core master data, demo users and roles.
- Seed only data required to make the newer modules visible/testable.
- Every insert is idempotent and keyed by a stable business code.
- No production HRM data is created by this pack.
===============================================================================
*/
:on error exit

USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Core master data / demo users. */
:r "06_Seed.sql"

/* ---------------------------------------------------------------------------
   Payroll: create the current company-cycle period (21st -> 20th).
--------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03PayrollCalculationPeriods',N'U') IS NOT NULL
BEGIN
    DECLARE @SeedAsOf date = CAST(GETDATE() AS date);
    DECLARE @SeedFrom date = CASE
        WHEN DAY(@SeedAsOf) >= 21
            THEN DATEFROMPARTS(YEAR(@SeedAsOf),MONTH(@SeedAsOf),21)
        ELSE DATEADD(MONTH,-1,DATEFROMPARTS(YEAR(@SeedAsOf),MONTH(@SeedAsOf),21))
    END;
    DECLARE @SeedTo date = DATEADD(DAY,-1,DATEADD(MONTH,1,@SeedFrom));
    DECLARE @SeedPeriodCode nvarchar(20) = CONVERT(nvarchar(10),@SeedFrom,23)+N'_'+CONVERT(nvarchar(10),@SeedTo,23);

    IF NOT EXISTS (SELECT 1 FROM dbo.F03PayrollCalculationPeriods WHERE PeriodCode=@SeedPeriodCode)
    BEGIN
        INSERT dbo.F03PayrollCalculationPeriods
        (IsActive,CreatedBy,CreatedAt,PeriodCode,FromDate,ToDate,Status,LastModifiedSource)
        VALUES
        (1,0,GETDATE(),@SeedPeriodCode,@SeedFrom,@SeedTo,N'Open',N'SEED');
    END;
END;
GO

/* ---------------------------------------------------------------------------
   Endpoint Governance: publish two baseline policies and representative items.
   These are catalog data, not endpoint findings. They make the catalog usable
   immediately after a test deployment without inventing real endpoint data.
--------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03EndpointGovernancePolicies',N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.F03EndpointGovernancePolicyItems',N'U') IS NOT NULL
BEGIN
    DECLARE @SoftwarePolicyId int;
    DECLARE @ServicePolicyId int;

    SELECT @SoftwarePolicyId=Id
    FROM dbo.F03EndpointGovernancePolicies
    WHERE PolicyCode=N'SEED-SOFTWARE-BASELINE' AND Version=1;

    IF @SoftwarePolicyId IS NULL
    BEGIN
        INSERT dbo.F03EndpointGovernancePolicies
        (PolicyCode,PolicyName,ItemType,TargetType,Version,IsPublished,EffectiveFromUtc,Remark,IsActive,CreatedBy,LastModifiedSource)
        VALUES
        (N'SEED-SOFTWARE-BASELINE',N'Demo Software Baseline',1,1,1,1,SYSUTCDATETIME(),N'Demo catalog seed; replace with company-approved catalog.',1,0,N'SEED');
        SET @SoftwarePolicyId=CONVERT(int,SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.F03EndpointGovernancePolicies
        SET IsPublished=1,IsActive=1
        WHERE Id=@SoftwarePolicyId;
    END;

    SELECT @ServicePolicyId=Id
    FROM dbo.F03EndpointGovernancePolicies
    WHERE PolicyCode=N'SEED-SERVICE-BASELINE' AND Version=1;

    IF @ServicePolicyId IS NULL
    BEGIN
        INSERT dbo.F03EndpointGovernancePolicies
        (PolicyCode,PolicyName,ItemType,TargetType,Version,IsPublished,EffectiveFromUtc,Remark,IsActive,CreatedBy,LastModifiedSource)
        VALUES
        (N'SEED-SERVICE-BASELINE',N'Demo Windows Service Baseline',2,1,1,1,SYSUTCDATETIME(),N'Demo catalog seed; replace with company-approved service policy.',1,0,N'SEED');
        SET @ServicePolicyId=CONVERT(int,SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.F03EndpointGovernancePolicies
        SET IsPublished=1,IsActive=1
        WHERE Id=@ServicePolicyId;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.F03EndpointGovernancePolicyItems
        WHERE PolicyId=@SoftwarePolicyId AND ItemType=1 AND NormalizedName=N'7-ZIP'
    )
    INSERT dbo.F03EndpointGovernancePolicyItems
    (PolicyId,ItemType,NormalizedName,DisplayName,Publisher,VersionConstraint,AliasNames,IsAllowed,Remark,IsActive,CreatedBy,LastModifiedSource)
    VALUES
    (@SoftwarePolicyId,1,N'7-ZIP',N'7-Zip',N'7-Zip',N'>=23.00',N'7-Zip|7zip|7-Zip File Manager',1,N'Demo software catalog item.',1,0,N'SEED');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.F03EndpointGovernancePolicyItems
        WHERE PolicyId=@SoftwarePolicyId AND ItemType=1 AND NormalizedName=N'GOOGLE CHROME'
    )
    INSERT dbo.F03EndpointGovernancePolicyItems
    (PolicyId,ItemType,NormalizedName,DisplayName,Publisher,VersionConstraint,AliasNames,IsAllowed,Remark,IsActive,CreatedBy,LastModifiedSource)
    VALUES
    (@SoftwarePolicyId,1,N'GOOGLE CHROME',N'Google Chrome',N'Google LLC',N'>=120',N'Google Chrome|Chrome',1,N'Demo software catalog item.',1,0,N'SEED');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.F03EndpointGovernancePolicyItems
        WHERE PolicyId=@ServicePolicyId AND ItemType=2 AND NormalizedName=N'W32TIME'
    )
    INSERT dbo.F03EndpointGovernancePolicyItems
    (PolicyId,ItemType,NormalizedName,DisplayName,Publisher,VersionConstraint,AliasNames,IsAllowed,Remark,IsActive,CreatedBy,LastModifiedSource)
    VALUES
    (@ServicePolicyId,2,N'W32TIME',N'Windows Time',N'Microsoft',NULL,N'W32Time|Windows Time',1,N'Demo Windows service catalog item.',1,0,N'SEED');
END;
GO

/* ---------------------------------------------------------------------------
   Endpoint demo inventory: one clearly identified synthetic endpoint linked
   to demo employee E0001. It is safe to delete by DeviceKey SEED-E0001.
--------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03EndpointDevices',N'U') IS NOT NULL
BEGIN
    DECLARE @SeedEndpointId bigint;

    SELECT @SeedEndpointId=Id
    FROM dbo.F03EndpointDevices
    WHERE DeviceKey=N'SEED-E0001';

    IF @SeedEndpointId IS NULL
    BEGIN
        INSERT dbo.F03EndpointDevices
        (DeviceKey,ComputerName,SerialNumber,HardwareUuid,AgentInstallationId,OsName,OsVersion,EmployeeCode,AgentVersion,LastSeenUtc,Status,IdentityStatus,LastInventoryHash,Source)
        VALUES
        (N'SEED-E0001',N'FVN-DEMO-E0001',N'SEED-SERIAL-E0001',N'SEED-UUID-E0001',N'SEED-INSTALL-E0001',N'Windows 11 Pro',N'23H2',N'E0001',N'1.0.0',SYSUTCDATETIME(),N'Online',N'Confirmed',N'SEED-HASH-E0001',N'SEED');
        SET @SeedEndpointId=CONVERT(bigint,SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.F03EndpointDevices
        SET EmployeeCode=N'E0001',Status=N'Online',IdentityStatus=N'Confirmed',LastSeenUtc=SYSUTCDATETIME(),Source=N'SEED'
        WHERE Id=@SeedEndpointId;
    END;

    IF OBJECT_ID(N'dbo.F03EndpointSoftwareInventory',N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.F03EndpointSoftwareInventory
           WHERE EndpointDeviceId=@SeedEndpointId AND NormalizedName=N'7-ZIP'
       )
    INSERT dbo.F03EndpointSoftwareInventory
    (EndpointDeviceId,NormalizedName,DisplayName,Publisher,Version,Architecture,InstallDate,InstallLocation,Source)
    VALUES
    (@SeedEndpointId,N'7-ZIP',N'7-Zip',N'7-Zip',N'24.09',N'x64',CAST(GETDATE() AS date),N'C:\\Program Files\\7-Zip',N'SEED');

    IF OBJECT_ID(N'dbo.F03EndpointSoftwareInventory',N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.F03EndpointSoftwareInventory
           WHERE EndpointDeviceId=@SeedEndpointId AND NormalizedName=N'GOOGLE CHROME'
       )
    INSERT dbo.F03EndpointSoftwareInventory
    (EndpointDeviceId,NormalizedName,DisplayName,Publisher,Version,Architecture,InstallDate,InstallLocation,Source)
    VALUES
    (@SeedEndpointId,N'GOOGLE CHROME',N'Google Chrome',N'Google LLC',N'140.0',N'x64',CAST(GETDATE() AS date),N'C:\\Program Files\\Google\\Chrome',N'SEED');

    IF OBJECT_ID(N'dbo.F03EndpointServiceInventory',N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.F03EndpointServiceInventory
           WHERE EndpointDeviceId=@SeedEndpointId AND ServiceName=N'W32Time'
       )
    INSERT dbo.F03EndpointServiceInventory
    (EndpointDeviceId,ServiceName,DisplayName,State,StartMode,BinaryPathHash,Source)
    VALUES
    (@SeedEndpointId,N'W32Time',N'Windows Time',N'Running',N'Auto',N'SEED-BINARY-HASH',N'SEED');
END;
GO

/* Final seed report. */
SELECT N'F03Employees' AS TableName, COUNT_BIG(*) AS SeededRows FROM dbo.F03Employees WHERE EmployeeCode IN (N'E0001',N'E0002',N'E0003',N'E0004',N'E0005')
UNION ALL
SELECT N'F03PayrollCalculationPeriods', COUNT_BIG(*) FROM dbo.F03PayrollCalculationPeriods WHERE LastModifiedSource=N'SEED'
UNION ALL
SELECT N'F03EndpointGovernancePolicies', COUNT_BIG(*) FROM dbo.F03EndpointGovernancePolicies WHERE PolicyCode LIKE N'SEED-%'
UNION ALL
SELECT N'F03EndpointGovernancePolicyItems', COUNT_BIG(*) FROM dbo.F03EndpointGovernancePolicyItems WHERE LastModifiedSource=N'SEED'
UNION ALL
SELECT N'F03EndpointDevices', COUNT_BIG(*) FROM dbo.F03EndpointDevices WHERE DeviceKey=N'SEED-E0001'
UNION ALL
SELECT N'F03EndpointSoftwareInventory', COUNT_BIG(*) FROM dbo.F03EndpointSoftwareInventory WHERE Source=N'SEED'
UNION ALL
SELECT N'F03EndpointServiceInventory', COUNT_BIG(*) FROM dbo.F03EndpointServiceInventory WHERE Source=N'SEED';

PRINT N'COMPLETE TEST SEED: core master data + demo users + payroll period + Endpoint catalog + demo endpoint inventory.';
GO

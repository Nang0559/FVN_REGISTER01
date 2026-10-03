/*
===============================================================================
FVN_REGISTER - Endpoint Agent data hardening
===============================================================================
- Publishes a deny-by-default baseline so an endpoint is never left with
  NO_ACTIVE_POLICY merely because the catalog has not been initialized.
- Software/service entries are still explicitly approved by IT before they are
  added to the published policy.
- Provides a retention procedure for resolved compliance findings.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2(0) = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM dbo.F03EndpointGovernancePolicies WHERE PolicyCode = N'EP-SW-BASELINE' AND Version = 1)
BEGIN
    INSERT INTO dbo.F03EndpointGovernancePolicies
    (
        PolicyCode, PolicyName, ItemType, TargetType, Version, IsPublished,
        EffectiveFromUtc, Remark, IsActive, CreatedBy, LastModifiedSource
    )
    VALUES
    (
        N'EP-SW-BASELINE', N'Endpoint Software Baseline', 1, 3, 1, 1,
        @Now, N'Deny-by-default baseline. Add and approve explicit software catalog items before allowing them.', 1, 0, N'SQL/60_EndpointAgentDataHardening'
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.F03EndpointGovernancePolicies WHERE PolicyCode = N'EP-SVC-BASELINE' AND Version = 1)
BEGIN
    INSERT INTO dbo.F03EndpointGovernancePolicies
    (
        PolicyCode, PolicyName, ItemType, TargetType, Version, IsPublished,
        EffectiveFromUtc, Remark, IsActive, CreatedBy, LastModifiedSource
    )
    VALUES
    (
        N'EP-SVC-BASELINE', N'Endpoint Windows Service Baseline', 2, 3, 1, 1,
        @Now, N'Deny-by-default baseline. Add and approve explicit Windows services before allowing them.', 1, 0, N'SQL/60_EndpointAgentDataHardening'
    );
END;

IF OBJECT_ID(N'dbo.usp_CleanupEndpointComplianceFindings', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.usp_CleanupEndpointComplianceFindings AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE dbo.usp_CleanupEndpointComplianceFindings
    @RetentionDays int = 180,
    @BatchSize int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RetentionDays < 30 SET @RetentionDays = 30;
    IF @BatchSize < 100 SET @BatchSize = 100;

    DECLARE @Cutoff datetime2(0) = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
    DECLARE @Deleted int = 1;

    WHILE @Deleted > 0
    BEGIN
        DELETE TOP (@BatchSize)
        FROM dbo.F03EndpointComplianceFindings
        WHERE ResolvedAtUtc IS NOT NULL
          AND ResolvedAtUtc < @Cutoff;
        SET @Deleted = @@ROWCOUNT;
    END;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointComplianceFindings_ResolvedRetention' AND object_id = OBJECT_ID(N'dbo.F03EndpointComplianceFindings'))
    CREATE INDEX IX_F03EndpointComplianceFindings_ResolvedRetention
        ON dbo.F03EndpointComplianceFindings(ResolvedAtUtc, EndpointDeviceId)
        WHERE ResolvedAtUtc IS NOT NULL;
GO

COMMIT TRANSACTION;
GO

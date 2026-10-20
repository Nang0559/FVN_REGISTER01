SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
/* FVN_REGISTER AI Platform - Phase 4-5 knowledge/active-learning migration. Apply after 008. Safe to execute repeatedly. */
IF OBJECT_ID(N'ai.AiGoldenDatasetCases', N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiGoldenDatasetCases
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiGoldenDatasetCases PRIMARY KEY,
        TenantCode NVARCHAR(64) NOT NULL, DatasetCode NVARCHAR(128) NOT NULL, CaseType NVARCHAR(64) NOT NULL,
        InputJson NVARCHAR(MAX) NOT NULL, ExpectedJson NVARCHAR(MAX) NOT NULL, RubricJson NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AiGoldenDatasetCases_IsActive DEFAULT(1), CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT CK_AiGoldenDatasetCases_InputJson CHECK (ISJSON(InputJson)=1),
        CONSTRAINT CK_AiGoldenDatasetCases_ExpectedJson CHECK (ISJSON(ExpectedJson)=1),
        CONSTRAINT CK_AiGoldenDatasetCases_RubricJson CHECK (RubricJson IS NULL OR ISJSON(RubricJson)=1)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiGoldenDatasetCases_TenantDataset' AND object_id=OBJECT_ID(N'ai.AiGoldenDatasetCases')) CREATE INDEX IX_AiGoldenDatasetCases_TenantDataset ON ai.AiGoldenDatasetCases(TenantCode, DatasetCode, IsActive);
GO
IF OBJECT_ID(N'ai.AiGoldenDatasetEvaluations', N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiGoldenDatasetEvaluations
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiGoldenDatasetEvaluations PRIMARY KEY, CaseId UNIQUEIDENTIFIER NOT NULL,
        ReleaseId UNIQUEIDENTIFIER NULL, Passed BIT NOT NULL, ActualJson NVARCHAR(MAX) NULL, FailureReason NVARCHAR(2000) NULL,
        EvaluatedAtUtc DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT FK_AiGoldenDatasetEvaluations_Case FOREIGN KEY(CaseId) REFERENCES ai.AiGoldenDatasetCases(Id),
        CONSTRAINT CK_AiGoldenDatasetEvaluations_ActualJson CHECK(ActualJson IS NULL OR ISJSON(ActualJson)=1)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiGoldenDatasetEvaluations_Case' AND object_id=OBJECT_ID(N'ai.AiGoldenDatasetEvaluations')) CREATE INDEX IX_AiGoldenDatasetEvaluations_Case ON ai.AiGoldenDatasetEvaluations(CaseId, EvaluatedAtUtc DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiGoldenDatasetEvaluations_Release' AND object_id=OBJECT_ID(N'ai.AiGoldenDatasetEvaluations')) CREATE INDEX IX_AiGoldenDatasetEvaluations_Release ON ai.AiGoldenDatasetEvaluations(ReleaseId, EvaluatedAtUtc DESC);
GO
IF OBJECT_ID(N'ai.AiModelReleases', N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiModelReleases
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiModelReleases PRIMARY KEY,
        TenantCode NVARCHAR(64) NOT NULL, ReleaseCode NVARCHAR(128) NOT NULL,
        ModelProvider NVARCHAR(64) NOT NULL, ModelVersion NVARCHAR(128) NOT NULL, PromptVersion NVARCHAR(128) NOT NULL,
        SchemaCode NVARCHAR(128) NULL, SchemaVersion INT NULL, GoldenDatasetCode NVARCHAR(128) NOT NULL,
        Status NVARCHAR(16) NOT NULL, MinimumAcceptanceRate FLOAT NOT NULL,
        CreatedAtUtc DATETIMEOFFSET(7) NOT NULL, PromotedAtUtc DATETIMEOFFSET(7) NULL, RetiredAtUtc DATETIMEOFFSET(7) NULL,
        CreatedBy NVARCHAR(128) NOT NULL,
        CONSTRAINT UQ_AiModelReleases_TenantCode_ReleaseCode UNIQUE(TenantCode, ReleaseCode),
        CONSTRAINT CK_AiModelReleases_Status CHECK(Status IN(N'Candidate',N'Active',N'Retired',N'RolledBack')),
        CONSTRAINT CK_AiModelReleases_Acceptance CHECK(MinimumAcceptanceRate >= 0 AND MinimumAcceptanceRate <= 1),
        CONSTRAINT CK_AiModelReleases_SchemaVersion CHECK(SchemaVersion IS NULL OR SchemaVersion > 0)
    );
END
GO
IF COL_LENGTH(N'ai.AiModelReleases', N'GoldenDatasetCode') IS NULL ALTER TABLE ai.AiModelReleases ADD GoldenDatasetCode NVARCHAR(128) NULL;
GO
IF EXISTS (SELECT 1 FROM ai.AiModelReleases WHERE GoldenDatasetCode IS NULL OR LTRIM(RTRIM(GoldenDatasetCode))='') UPDATE ai.AiModelReleases SET GoldenDatasetCode=ReleaseCode WHERE GoldenDatasetCode IS NULL OR LTRIM(RTRIM(GoldenDatasetCode))='';
GO
IF COL_LENGTH(N'ai.AiModelReleases', N'GoldenDatasetCode') IS NOT NULL ALTER TABLE ai.AiModelReleases ALTER COLUMN GoldenDatasetCode NVARCHAR(128) NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiModelReleases_Active' AND object_id=OBJECT_ID(N'ai.AiModelReleases')) CREATE INDEX IX_AiModelReleases_Active ON ai.AiModelReleases(TenantCode, Status, PromotedAtUtc DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_AiModelReleases_OneActive' AND object_id=OBJECT_ID(N'ai.AiModelReleases')) CREATE UNIQUE INDEX UX_AiModelReleases_OneActive ON ai.AiModelReleases(TenantCode) WHERE Status=N'Active';
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiGoldenDatasetEvaluations_Release' AND parent_object_id=OBJECT_ID(N'ai.AiGoldenDatasetEvaluations'))
    ALTER TABLE ai.AiGoldenDatasetEvaluations ADD CONSTRAINT FK_AiGoldenDatasetEvaluations_Release FOREIGN KEY(ReleaseId) REFERENCES ai.AiModelReleases(Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_AiKnowledgeChunks_AccessScopeJson' AND parent_object_id=OBJECT_ID(N'ai.AiKnowledgeChunks')) ALTER TABLE ai.AiKnowledgeChunks ADD CONSTRAINT CK_AiKnowledgeChunks_AccessScopeJson CHECK(ISJSON(AccessScopeJson)=1);
GO

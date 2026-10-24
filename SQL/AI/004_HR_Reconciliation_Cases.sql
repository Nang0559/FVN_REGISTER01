IF OBJECT_ID(N'ai.AiHRCases',N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiHRCases
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiHRCases PRIMARY KEY,
        TenantCode NVARCHAR(64) NOT NULL,
        EmployeeCode NVARCHAR(64) NOT NULL,
        CaseType NVARCHAR(24) NOT NULL,
        WorkDate DATE NOT NULL,
        Outcome NVARCHAR(32) NOT NULL,
        UserStatement NVARCHAR(MAX) NOT NULL,
        EvidenceReference NVARCHAR(512) NULL,
        EvidenceCandidateJson NVARCHAR(MAX) NULL,
        AnalysisJson NVARCHAR(MAX) NOT NULL,
        SuggestedReply NVARCHAR(4000) NOT NULL,
        InternalHRNote NVARCHAR(MAX) NOT NULL,
        OfficialSource NVARCHAR(128) NOT NULL,
        OfficialRecordReference NVARCHAR(256) NOT NULL,
        FactsRetrievedAtUtc DATETIMEOFFSET(7) NOT NULL,
        Status NVARCHAR(24) NOT NULL CONSTRAINT DF_AiHRCases_Status DEFAULT(N'PendingHRReview'),
        CreatedBy NVARCHAR(128) NOT NULL,
        CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
        ReviewedBy NVARCHAR(128) NULL,
        ReviewDecision NVARCHAR(16) NULL,
        FinalReply NVARCHAR(4000) NULL,
        ReviewComment NVARCHAR(2000) NULL,
        ReviewedAtUtc DATETIMEOFFSET(7) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_AiHRCases_CaseType CHECK(CaseType IN(N'Overtime',N'Attendance',N'Leave')),
        CONSTRAINT CK_AiHRCases_Status CHECK(Status IN(N'PendingHRReview',N'Approved',N'Rejected')),
        CONSTRAINT CK_AiHRCases_Outcome CHECK(Outcome IN(N'Matched',N'Discrepancy',N'InsufficientEvidence',N'NeedsHumanDecision')),
        CONSTRAINT CK_AiHRCases_ReviewDecision CHECK(ReviewDecision IS NULL OR ReviewDecision IN(N'Approved',N'Rejected',N'Edited')),
        CONSTRAINT CK_AiHRCases_AnalysisJson CHECK(ISJSON(AnalysisJson)=1)
    );
    CREATE INDEX IX_AiHRCases_Tenant_WorkDate_Type
        ON ai.AiHRCases(TenantCode, WorkDate, CaseType);
    CREATE INDEX IX_AiHRCases_Tenant_Status_Created
        ON ai.AiHRCases(TenantCode, Status, CreatedAtUtc DESC);
END
GO

IF OBJECT_ID(N'ai.AiHRCases',N'U') IS NOT NULL AND COL_LENGTH(N'ai.AiHRCases',N'EvidenceCandidateJson') IS NULL
BEGIN
    ALTER TABLE ai.AiHRCases ADD EvidenceCandidateJson NVARCHAR(MAX) NULL;
END
GO

IF OBJECT_ID(N'ai.AiHRCases',N'U') IS NOT NULL AND COL_LENGTH(N'ai.AiHRCases',N'RowVersion') IS NULL
BEGIN
    ALTER TABLE ai.AiHRCases ADD RowVersion ROWVERSION NOT NULL;
END
GO

IF OBJECT_ID(N'ai.AiHRCaseReviews',N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiHRCaseReviews
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiHRCaseReviews PRIMARY KEY,
        CaseId UNIQUEIDENTIFIER NOT NULL,
        TenantCode NVARCHAR(64) NOT NULL,
        ReviewerId NVARCHAR(128) NOT NULL,
        Decision NVARCHAR(16) NOT NULL,
        FinalReply NVARCHAR(4000) NULL,
        Comment NVARCHAR(2000) NULL,
        ReviewedAtUtc DATETIMEOFFSET(7) NOT NULL,
        CONSTRAINT FK_AiHRCaseReviews_AiHRCases FOREIGN KEY(CaseId)
            REFERENCES ai.AiHRCases(Id),
        CONSTRAINT CK_AiHRCaseReviews_Decision CHECK(Decision IN(N'Approved',N'Rejected',N'Edited'))
    );
    CREATE INDEX IX_AiHRCaseReviews_Tenant_Case_Reviewed
        ON ai.AiHRCaseReviews(TenantCode, CaseId, ReviewedAtUtc DESC);
END
GO

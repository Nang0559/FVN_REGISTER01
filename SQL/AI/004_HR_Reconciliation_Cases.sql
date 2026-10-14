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
        UserStatement NVARCHAR(8000) NOT NULL,
        EvidenceReference NVARCHAR(512) NULL,
        AnalysisJson NVARCHAR(MAX) NOT NULL,
        SuggestedReply NVARCHAR(4000) NOT NULL,
        InternalHRNote NVARCHAR(8000) NOT NULL,
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

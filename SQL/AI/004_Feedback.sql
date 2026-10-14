IF OBJECT_ID(N'ai.AiFeedback',N'U') IS NULL
CREATE TABLE ai.AiFeedback(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 TenantCode NVARCHAR(64) NOT NULL,
 DocumentId UNIQUEIDENTIFIER NULL,
 ExtractionId UNIQUEIDENTIFIER NULL,
 ReviewerId NVARCHAR(128) NOT NULL,
 FeedbackType NVARCHAR(24) NOT NULL,
 BeforeJson NVARCHAR(MAX) NULL,
 AfterJson NVARCHAR(MAX) NULL,
 Reason NVARCHAR(2000) NULL,
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiFeedback_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
 CONSTRAINT CK_AiFeedback_Type CHECK(FeedbackType IN(N'Accept',N'Reject',N'Correct',N'AnswerRating')),
 CONSTRAINT CK_AiFeedback_BeforeJson CHECK(BeforeJson IS NULL OR ISJSON(BeforeJson)=1),
 CONSTRAINT CK_AiFeedback_AfterJson CHECK(AfterJson IS NULL OR ISJSON(AfterJson)=1)
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiFeedback') AND name=N'IX_AiFeedback_Tenant_Created')
 CREATE INDEX IX_AiFeedback_Tenant_Created ON ai.AiFeedback(TenantCode,CreatedAtUtc DESC);
GO
IF OBJECT_ID(N'ai.AiExtractions',N'U') IS NULL
CREATE TABLE ai.AiExtractions(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 DocumentId UNIQUEIDENTIFIER NOT NULL,
 TenantCode NVARCHAR(64) NOT NULL,
 DocumentType NVARCHAR(128) NOT NULL,
 ClassificationConfidence DECIMAL(6,5) NOT NULL,
 CandidateJson NVARCHAR(MAX) NOT NULL,
 ParserVersion NVARCHAR(64) NOT NULL,
 ModelProvider NVARCHAR(64) NULL,
 ModelVersion NVARCHAR(128) NULL,
 PromptVersion NVARCHAR(64) NULL,
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiExtractions_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
 CONSTRAINT CK_AiExtractions_Confidence CHECK(ClassificationConfidence BETWEEN 0 AND 1),
 CONSTRAINT CK_AiExtractions_Json CHECK(ISJSON(CandidateJson)=1)
);
GO
IF OBJECT_ID(N'ai.AiValidationIssues',N'U') IS NULL
CREATE TABLE ai.AiValidationIssues(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 ExtractionId UNIQUEIDENTIFIER NOT NULL,
 FieldPath NVARCHAR(256) NULL,
 RuleCode NVARCHAR(128) NOT NULL,
 Severity NVARCHAR(16) NOT NULL,
 Message NVARCHAR(1000) NOT NULL,
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiValidationIssues_Extraction FOREIGN KEY(ExtractionId) REFERENCES ai.AiExtractions(Id),
 CONSTRAINT CK_AiValidationIssues_Severity CHECK(Severity IN(N'Info',N'Warning',N'Error'))
);
GO
IF OBJECT_ID(N'ai.AiReviews',N'U') IS NULL
CREATE TABLE ai.AiReviews(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 ExtractionId UNIQUEIDENTIFIER NOT NULL,
 DocumentId UNIQUEIDENTIFIER NOT NULL,
 TenantCode NVARCHAR(64) NOT NULL,
 ReviewerId NVARCHAR(128) NOT NULL,
 Decision NVARCHAR(16) NOT NULL,
 OriginalPayloadJson NVARCHAR(MAX) NOT NULL,
 CorrectedPayloadJson NVARCHAR(MAX) NULL,
 Comment NVARCHAR(2000) NULL,
 ReviewedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiReviews_Extraction FOREIGN KEY(ExtractionId) REFERENCES ai.AiExtractions(Id),
 CONSTRAINT FK_AiReviews_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
 CONSTRAINT CK_AiReviews_Decision CHECK(Decision IN(N'Approved',N'Rejected',N'Corrected')),
 CONSTRAINT CK_AiReviews_OriginalJson CHECK(ISJSON(OriginalPayloadJson)=1),
 CONSTRAINT CK_AiReviews_CorrectedJson CHECK(CorrectedPayloadJson IS NULL OR ISJSON(CorrectedPayloadJson)=1)
);
GO
IF OBJECT_ID(N'ai.AiKnowledgeChunks',N'U') IS NULL
CREATE TABLE ai.AiKnowledgeChunks(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 TenantCode NVARCHAR(64) NOT NULL,
 DocumentId UNIQUEIDENTIFIER NOT NULL,
 ReviewId UNIQUEIDENTIFIER NOT NULL,
 ChunkOrdinal INT NOT NULL,
 PageNumber INT NULL,
 SectionPath NVARCHAR(512) NULL,
 Content NVARCHAR(MAX) NOT NULL,
 ContentHash NVARCHAR(64) NOT NULL,
 AccessScopeJson NVARCHAR(MAX) NOT NULL,
 EmbeddingModel NVARCHAR(128) NULL,
 EmbeddingJson NVARCHAR(MAX) NULL,
 IsActive BIT NOT NULL CONSTRAINT DF_AiKnowledgeChunks_IsActive DEFAULT(1),
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiKnowledgeChunks_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
 CONSTRAINT FK_AiKnowledgeChunks_Review FOREIGN KEY(ReviewId) REFERENCES ai.AiReviews(Id),
 CONSTRAINT CK_AiKnowledgeChunks_ScopeJson CHECK(ISJSON(AccessScopeJson)=1),
 CONSTRAINT CK_AiKnowledgeChunks_EmbeddingJson CHECK(EmbeddingJson IS NULL OR ISJSON(EmbeddingJson)=1)
);
GO
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
 ParserVersion NVARCHAR(64) NULL,
 ModelVersion NVARCHAR(128) NULL,
 PromptVersion NVARCHAR(64) NULL,
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 CONSTRAINT FK_AiFeedback_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
 CONSTRAINT FK_AiFeedback_Extraction FOREIGN KEY(ExtractionId) REFERENCES ai.AiExtractions(Id),
 CONSTRAINT CK_AiFeedback_Type CHECK(FeedbackType IN(N'Accept',N'Reject',N'Correct',N'AnswerRating')),
 CONSTRAINT CK_AiFeedback_BeforeJson CHECK(BeforeJson IS NULL OR ISJSON(BeforeJson)=1),
 CONSTRAINT CK_AiFeedback_AfterJson CHECK(AfterJson IS NULL OR ISJSON(AfterJson)=1)
);
GO
IF OBJECT_ID(N'ai.AiModelDeployments',N'U') IS NULL
CREATE TABLE ai.AiModelDeployments(
 Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 Provider NVARCHAR(64) NOT NULL,
 ModelName NVARCHAR(128) NOT NULL,
 ModelVersion NVARCHAR(128) NOT NULL,
 TaskType NVARCHAR(64) NOT NULL,
 ConfigurationJson NVARCHAR(MAX) NOT NULL,
 AllowedDataClassesJson NVARCHAR(MAX) NOT NULL,
 Status NVARCHAR(16) NOT NULL,
 CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
 ActivatedAtUtc DATETIMEOFFSET(7) NULL,
 RetiredAtUtc DATETIMEOFFSET(7) NULL,
 CONSTRAINT CK_AiModelDeployments_Status CHECK(Status IN(N'Draft',N'Active',N'Retired',N'Disabled')),
 CONSTRAINT CK_AiModelDeployments_Config CHECK(ISJSON(ConfigurationJson)=1 AND ISJSON(AllowedDataClassesJson)=1)
);
GO

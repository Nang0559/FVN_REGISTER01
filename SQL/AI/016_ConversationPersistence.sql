/*
 016 - Tenant/actor-bound AI conversations.
 Messages are append-only; each user turn stores the strict business-context snapshot
 used for that turn. Conversation ownership is checked on every API operation.
*/
IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO
IF OBJECT_ID(N'ai.AiConversations',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiConversations(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiConversations PRIMARY KEY,
  TenantCode NVARCHAR(64) NOT NULL,
  ActorId NVARCHAR(128) NOT NULL,
  Title NVARCHAR(200) NOT NULL,
  Status NVARCHAR(16) NOT NULL CONSTRAINT DF_AiConversations_Status DEFAULT(N'Active'),
  ContextVersion NVARCHAR(64) NULL,
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  UpdatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  ExpiresAtUtc DATETIMEOFFSET(7) NOT NULL,
  CONSTRAINT CK_AiConversations_Status CHECK(Status IN(N'Active',N'Archived'))
 );
 CREATE INDEX IX_AiConversations_Owner_Updated ON ai.AiConversations(TenantCode,ActorId,UpdatedAtUtc DESC);
 CREATE UNIQUE INDEX UX_AiConversations_Owner_Id ON ai.AiConversations(TenantCode,Id,ActorId);
END;
GO
IF OBJECT_ID(N'ai.AiConversationMessages',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiConversationMessages(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiConversationMessages PRIMARY KEY,
  ConversationId UNIQUEIDENTIFIER NOT NULL,
  TenantCode NVARCHAR(64) NOT NULL,
  ActorId NVARCHAR(128) NOT NULL,
  Sequence INT NOT NULL,
  Role NVARCHAR(16) NOT NULL,
  Content NVARCHAR(MAX) NOT NULL,
  Status NVARCHAR(24) NOT NULL CONSTRAINT DF_AiConversationMessages_Status DEFAULT(N'Completed'),
  ContextJson NVARCHAR(MAX) NULL,
  CitationsJson NVARCHAR(MAX) NULL,
  ModelProvider NVARCHAR(64) NULL,
  ModelVersion NVARCHAR(128) NULL,
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  CONSTRAINT FK_AiConversationMessages_ConversationOwner FOREIGN KEY(TenantCode,ConversationId,ActorId) REFERENCES ai.AiConversations(TenantCode,Id,ActorId) ON DELETE CASCADE,
  CONSTRAINT CK_AiConversationMessages_Sequence CHECK(Sequence>0),
  CONSTRAINT CK_AiConversationMessages_Role CHECK(Role IN(N'user',N'assistant',N'system')),
  CONSTRAINT CK_AiConversationMessages_ContextJson CHECK(ContextJson IS NULL OR ISJSON(ContextJson)=1),
  CONSTRAINT CK_AiConversationMessages_CitationsJson CHECK(CitationsJson IS NULL OR ISJSON(CitationsJson)=1),
  CONSTRAINT UQ_AiConversationMessages_Sequence UNIQUE(TenantCode,ConversationId,Sequence)
 );
 CREATE INDEX IX_AiConversationMessages_Owner_Conversation ON ai.AiConversationMessages(TenantCode,ActorId,ConversationId,Sequence);
END;
GO

/*
 Run from a controlled SQL Agent schedule. The API also hides expired sessions;
 this procedure physically removes expired conversations and their messages.
*/
CREATE OR ALTER PROCEDURE ai.usp_PurgeExpiredConversations
    @BatchSize INT = 500
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @BatchSize < 1 OR @BatchSize > 5000
        THROW 51016, 'BatchSize must be between 1 and 5000.', 1;

    DECLARE @Expired TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
    INSERT INTO @Expired(Id)
    SELECT TOP (@BatchSize) Id
    FROM ai.AiConversations WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE ExpiresAtUtc <= TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
    ORDER BY ExpiresAtUtc;

    DELETE c
    FROM ai.AiConversations AS c
    INNER JOIN @Expired AS e ON e.Id = c.Id;

    SELECT @@ROWCOUNT AS DeletedConversations;
END;
GO

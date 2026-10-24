/*
 FVN_REGISTER AI Platform - persistent request replay protection.
 Apply after migration 010 to the dedicated FVN_AI database.
 Safe to execute repeatedly.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO

IF OBJECT_ID(N'ai.AiRequestNonces', N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiRequestNonces
    (
        TenantCode NVARCHAR(64) NOT NULL,
        Nonce NVARCHAR(128) NOT NULL,
        ExpiresAtUtc DATETIMEOFFSET(7) NOT NULL,
        CreatedAtUtc DATETIMEOFFSET(7) NOT NULL
            CONSTRAINT DF_AiRequestNonces_CreatedAtUtc DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT PK_AiRequestNonces PRIMARY KEY (TenantCode, Nonce)
    );
    CREATE INDEX IX_AiRequestNonces_ExpiresAtUtc
        ON ai.AiRequestNonces(ExpiresAtUtc);
END;
GO

IF OBJECT_ID(N'ai.AiSchemaMigrations', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId=N'011_Persistent_Request_Replay_Protection')
    INSERT INTO ai.AiSchemaMigrations(MigrationId)
    VALUES(N'011_Persistent_Request_Replay_Protection');
GO

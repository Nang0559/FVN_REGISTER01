USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
GO

/*
===============================================================================
64_WebPushSubscriptions
===============================================================================
Web Push subscriptions per user/device, used to push the unread-notification
count to the installed PWA (app icon badge) even when the app is closed and
the user is no longer signed in.

One row per browser push endpoint. Signing in on the same device as another
user re-binds the row to the new user (UserId is updated).
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03PushSubscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03PushSubscriptions
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PushSubscriptions PRIMARY KEY,
        IsActive bit NULL CONSTRAINT DF_F03PushSubscriptions_IsActive DEFAULT 1,
        CreatedBy int NOT NULL CONSTRAINT DF_F03PushSubscriptions_CreatedBy DEFAULT 0,
        LastModifiedSource nvarchar(50) NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03PushSubscriptions_CreatedAt DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,

        UserId int NOT NULL,
        EndpointHash char(64) NOT NULL,      -- SHA-256 hex of Endpoint (index key limit)
        Endpoint nvarchar(1000) NOT NULL,
        P256dh nvarchar(200) NOT NULL,
        Auth nvarchar(100) NOT NULL,
        UserAgent nvarchar(300) NULL,
        LastUsedAt datetime2(0) NOT NULL CONSTRAINT DF_F03PushSubscriptions_LastUsedAt DEFAULT GETDATE()
    );
END;
GO

IF OBJECT_ID(N'dbo.F03PushSubscriptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03PushSubscriptions_EndpointHash'
                   AND object_id = OBJECT_ID(N'dbo.F03PushSubscriptions'))
    CREATE UNIQUE INDEX UX_F03PushSubscriptions_EndpointHash ON dbo.F03PushSubscriptions(EndpointHash);
GO

IF OBJECT_ID(N'dbo.F03PushSubscriptions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03PushSubscriptions_User'
                   AND object_id = OBJECT_ID(N'dbo.F03PushSubscriptions'))
    CREATE INDEX IX_F03PushSubscriptions_User ON dbo.F03PushSubscriptions(UserId) WHERE IsActive = 1;
GO

PRINT N'64_WebPushSubscriptions applied.';
GO

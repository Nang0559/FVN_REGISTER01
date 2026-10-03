USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
===============================================================================
FVN_REGISTER - F03Users Two-Factor Authentication schema
===============================================================================
Canonical user-level 2FA state used by the authentication/security services.
This migration is idempotent and is safe for both new and existing databases.

Columns:
  TwoFactorEnabled          - user has completed/activated 2FA
  TwoFactorEnabledAt        - UTC timestamp when 2FA was activated
  TwoFactorRequired         - SuperAdmin/security policy requires this user to
                              configure and use 2FA
  TwoFactorRequiredAt       - UTC timestamp when the requirement was applied
  TwoFactorRequiredBy       - user Id of the administrator who applied it
  TwoFactorSecretEncrypted  - encrypted TOTP secret; never store plaintext
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03Users', N'U') IS NULL
    THROW 51500, N'F03Users is required before applying 14A_SecurityTwoFactorColumns.', 1;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorEnabled bit NOT NULL
            CONSTRAINT DF_F03Users_TwoFactorEnabled DEFAULT (0);
END;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorEnabledAt') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorEnabledAt datetime2(0) NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorRequired') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorRequired bit NOT NULL
            CONSTRAINT DF_F03Users_TwoFactorRequired DEFAULT (0);
END;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorRequiredAt') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorRequiredAt datetime2(0) NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorRequiredBy') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorRequiredBy int NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03Users', N'TwoFactorSecretEncrypted') IS NULL
BEGIN
    ALTER TABLE dbo.F03Users
        ADD TwoFactorSecretEncrypted nvarchar(2048) NULL;
END;
GO

/*
  Existing users are not silently forced into 2FA by this schema migration.
  Enforcement is controlled by TwoFactorRequired, which is intentionally left
  at the default value 0 until a SuperAdmin explicitly requires it.
*/

PRINT N'F03Users 2FA columns verified successfully.';
GO

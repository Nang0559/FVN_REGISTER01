/*
===============================================================================
FVN_REGISTER - Canonical company/national holiday classification
===============================================================================
F03CompanyHolidays is the canonical source for calendar holiday type.

HolidayType:
  1 = COMPANY       Nghỉ công ty
  2 = NATIONAL      Nghỉ lễ quốc gia
  3 = COMPENSATORY  Nghỉ bù
  4 = OTHER         Nghỉ khác

TinhPhep remains the leave-entitlement flag and is intentionally NOT reused
for holiday classification.

This script is idempotent and supports both existing and new databases.
The final schema is:
  Id, IsActive, CreatedBy, LastModifiedSource, CreatedAt, ModifiedBy,
  ModifiedAt, HolidayDate, Description, Year, TinhPhep, HolidayType.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ---------------------------------------------------------------------------
   1. Compatibility for the historical singular table name.
   Rename first, before attempting canonical creation.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03CompanyHolidays', N'U') IS NULL
   AND OBJECT_ID(N'dbo.F03CompanyHoliday', N'U') IS NOT NULL
BEGIN
    EXEC sys.sp_rename N'dbo.F03CompanyHoliday', N'F03CompanyHolidays';
END;

/* ---------------------------------------------------------------------------
   2. Canonical table creation when neither legacy nor canonical table exists.
   This guarantees that a fresh deployment also receives HolidayType.
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03CompanyHolidays', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03CompanyHolidays
    (
        Id int IDENTITY(1,1) NOT NULL,
        IsActive bit NULL
            CONSTRAINT DF_F03CompanyHolidays_IsActive DEFAULT ((1)),
        CreatedBy int NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_CreatedBy DEFAULT ((0)),
        LastModifiedSource nvarchar(50) NULL,
        CreatedAt datetime2(0) NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_CreatedAt DEFAULT (GETDATE()),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        HolidayDate date NOT NULL,
        Description nvarchar(200) NOT NULL,
        Year int NOT NULL,
        TinhPhep bit NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_TinhPhep DEFAULT ((1)),
        HolidayType tinyint NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_HolidayType DEFAULT ((1)),

        CONSTRAINT PK_F03CompanyHolidays PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT CK_F03CompanyHolidays_HolidayType
            CHECK (HolidayType IN (1,2,3,4))
    );
END;

/* ---------------------------------------------------------------------------
   3. Normalize legacy column name IsPaidLeave -> TinhPhep.
   --------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'TinhPhep') IS NULL
   AND COL_LENGTH(N'dbo.F03CompanyHolidays', N'IsPaidLeave') IS NOT NULL
BEGIN
    EXEC sys.sp_rename N'dbo.F03CompanyHolidays.IsPaidLeave', N'TinhPhep', N'COLUMN';
END;

/* ---------------------------------------------------------------------------
   4. Upgrade an existing table to the canonical HolidayType column.
   Existing rows deliberately default to COMPANY (1). Classification as
   NATIONAL/COMPENSATORY/OTHER must be explicit administrator data.
   --------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayType') IS NULL
BEGIN
    ALTER TABLE dbo.F03CompanyHolidays
        ADD HolidayType tinyint NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_HolidayType DEFAULT ((1)) WITH VALUES;
END;

/* ---------------------------------------------------------------------------
   5. Ensure the default constraint exists for legacy HolidayType columns.
   --------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayType') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.default_constraints dc
       INNER JOIN sys.columns c
           ON c.object_id = dc.parent_object_id
          AND c.column_id = dc.parent_column_id
       WHERE dc.parent_object_id = OBJECT_ID(N'dbo.F03CompanyHolidays')
         AND c.name = N'HolidayType'
   )
BEGIN
    ALTER TABLE dbo.F03CompanyHolidays
        ADD CONSTRAINT DF_F03CompanyHolidays_HolidayType DEFAULT ((1)) FOR HolidayType;
END;

/* ---------------------------------------------------------------------------
   6. Ensure the allowed-value constraint exists.
   --------------------------------------------------------------------------- */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.F03CompanyHolidays')
      AND name = N'CK_F03CompanyHolidays_HolidayType'
)
BEGIN
    ALTER TABLE dbo.F03CompanyHolidays
        ADD CONSTRAINT CK_F03CompanyHolidays_HolidayType
        CHECK (HolidayType IN (1,2,3,4));
END;

/* ---------------------------------------------------------------------------
   7. Normalize invalid/null historical values to COMPANY.
   Do not infer NATIONAL from TinhPhep or Description.
   --------------------------------------------------------------------------- */
UPDATE dbo.F03CompanyHolidays
SET HolidayType = 1
WHERE HolidayType IS NULL
   OR HolidayType NOT IN (1,2,3,4);

/* ---------------------------------------------------------------------------
   8. Final schema gate.
   --------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayDate') IS NULL
   OR COL_LENGTH(N'dbo.F03CompanyHolidays', N'Description') IS NULL
   OR COL_LENGTH(N'dbo.F03CompanyHolidays', N'Year') IS NULL
   OR COL_LENGTH(N'dbo.F03CompanyHolidays', N'TinhPhep') IS NULL
   OR COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayType') IS NULL
BEGIN
    THROW 51402, N'F03CompanyHolidays không đúng schema chuẩn: cần HolidayDate, Description, Year, TinhPhep và HolidayType.', 1;
END;

PRINT N'F03CompanyHolidays schema is canonical: 1=Company, 2=National, 3=Compensatory, 4=Other.';
GO

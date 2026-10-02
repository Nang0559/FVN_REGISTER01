/*
===============================================================================
FVN_REGISTER - Calendar holiday classification
===============================================================================
HolidayType:
  1 = COMPANY       Nghỉ công ty
  2 = NATIONAL      Nghỉ lễ quốc gia
  3 = COMPENSATORY  Nghỉ bù
  4 = OTHER         Nghỉ khác

TinhPhep remains the leave-entitlement flag and is intentionally NOT reused
for holiday classification.
===============================================================================
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.F03CompanyHolidays', N'U') IS NULL
    THROW 51401, N'F03CompanyHolidays chưa tồn tại. Hãy chạy phần Work Calendar trước.', 1;

IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayType') IS NULL
BEGIN
    ALTER TABLE dbo.F03CompanyHolidays
        ADD HolidayType tinyint NOT NULL
            CONSTRAINT DF_F03CompanyHolidays_HolidayType DEFAULT 1 WITH VALUES;
END;

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

/* Keep existing data valid. National/company classification is explicit data;
   do not overload TinhPhep. Existing rows default to COMPANY and must be
   changed to NATIONAL/COMPENSATORY/OTHER where appropriate by the administrator. */
UPDATE dbo.F03CompanyHolidays
SET HolidayType = 1
WHERE HolidayType IS NULL OR HolidayType NOT IN (1,2,3,4);

PRINT N'F03CompanyHolidays.HolidayType is ready: 1=Company, 2=National, 3=Compensatory, 4=Other.';
GO

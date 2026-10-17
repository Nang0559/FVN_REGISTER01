/*
    Normalize overtime categories to the three company pay rules:
      WD      = ordinary working day (Mon-Sat unless HRM marks a rest day): 150%
      RESTDAY = company-declared day off (HRM 001) or Sunday (HRM 002): 200%
      HOL     = public holiday (HRM 003): 300%
    Keep historical OT requests unchanged; only normalize the active lookup.
    Idempotent.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.F03OTTypes', N'U') IS NULL
    THROW 51001, 'Missing dbo.F03OTTypes; run the OT schema migrations first.', 1;

UPDATE dbo.F03OTTypes
SET OTTypeName = N'OT ngày thường (150%)',
    OTTypeName2 = N'OT ngày thường (150%)',
    RateMultiplier = 1.5,
    HRMCode = N'OT-WD',
    IsActive = 1
WHERE OTTypeCode = N'WD';

IF NOT EXISTS (SELECT 1 FROM dbo.F03OTTypes WHERE OTTypeCode = N'WD')
    INSERT dbo.F03OTTypes(IsActive, CreatedBy, OTTypeCode, OTTypeName, OTTypeName2, RateMultiplier, HRMCode)
    VALUES (1, 0, N'WD', N'OT ngày thường (150%)', N'OT ngày thường (150%)', 1.5, N'OT-WD');

UPDATE dbo.F03OTTypes
SET OTTypeName = N'OT ngày nghỉ công ty / Chủ nhật (200%)',
    OTTypeName2 = N'OT ngày nghỉ công ty / Chủ nhật (200%)',
    RateMultiplier = 2.0,
    HRMCode = N'OT-RESTDAY',
    IsActive = 1
WHERE OTTypeCode = N'RESTDAY';

IF NOT EXISTS (SELECT 1 FROM dbo.F03OTTypes WHERE OTTypeCode = N'RESTDAY')
    INSERT dbo.F03OTTypes(IsActive, CreatedBy, OTTypeCode, OTTypeName, OTTypeName2, RateMultiplier, HRMCode)
    VALUES (1, 0, N'RESTDAY', N'OT ngày nghỉ công ty / Chủ nhật (200%)', N'OT ngày nghỉ công ty / Chủ nhật (200%)', 2.0, N'OT-RESTDAY');

UPDATE dbo.F03OTTypes
SET OTTypeName = N'OT ngày lễ (300%)',
    OTTypeName2 = N'OT ngày lễ (300%)',
    RateMultiplier = 3.0,
    HRMCode = N'OT-HOL',
    IsActive = 1
WHERE OTTypeCode = N'HOL';

IF NOT EXISTS (SELECT 1 FROM dbo.F03OTTypes WHERE OTTypeCode = N'HOL')
    INSERT dbo.F03OTTypes(IsActive, CreatedBy, OTTypeCode, OTTypeName, OTTypeName2, RateMultiplier, HRMCode)
    VALUES (1, 0, N'HOL', N'OT ngày lễ (300%)', N'OT ngày lễ (300%)', 3.0, N'OT-HOL');

-- SAT, SUN and NIGHT are legacy categories; their historical requests remain intact.
UPDATE dbo.F03OTTypes SET IsActive = 0 WHERE OTTypeCode IN (N'SAT', N'SUN', N'NIGHT');

COMMIT TRANSACTION;
GO

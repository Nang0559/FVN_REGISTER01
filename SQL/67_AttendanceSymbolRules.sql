IF OBJECT_ID(N'dbo.F03AttendanceSymbolRules',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.F03AttendanceSymbolRules(
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03AttendanceSymbolRules PRIMARY KEY,
  RuleCode nvarchar(100) NOT NULL,RuleName nvarchar(200) NOT NULL,DayType nvarchar(40) NOT NULL,
  ShiftCode nvarchar(50) NULL,ShiftId int NULL,Priority int NOT NULL CONSTRAINT DF_F03AttendanceSymbolRules_Priority DEFAULT(100),
  MinActualMinutes int NULL,MaxActualMinutes int NULL,CheckInFrom varchar(5) NULL,CheckInTo varchar(5) NULL,
  EffectiveFrom date NULL,EffectiveTo date NULL,RuleJson nvarchar(max) NOT NULL,
  IsActive bit NOT NULL CONSTRAINT DF_F03AttendanceSymbolRules_IsActive DEFAULT(1),CreatedBy int NOT NULL CONSTRAINT DF_F03AttendanceSymbolRules_CreatedBy DEFAULT(0),
  CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03AttendanceSymbolRules_CreatedAt DEFAULT(SYSUTCDATETIME()),ModifiedBy int NULL,ModifiedAt datetime2(0) NULL,LastModifiedSource nvarchar(50) NULL);
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_F03AttendanceSymbolRules_Code' AND object_id=OBJECT_ID(N'dbo.F03AttendanceSymbolRules')) CREATE UNIQUE INDEX UX_F03AttendanceSymbolRules_Code ON dbo.F03AttendanceSymbolRules(RuleCode);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_F03AttendanceSymbolRules_Match' AND object_id=OBJECT_ID(N'dbo.F03AttendanceSymbolRules')) CREATE INDEX IX_F03AttendanceSymbolRules_Match ON dbo.F03AttendanceSymbolRules(IsActive,DayType,ShiftCode,ShiftId,Priority,EffectiveFrom,EffectiveTo);
GO

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 53001, N'F03Functions is required before seeding WorkCalendar.SymbolRuleManage.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 3044)
BEGIN
    IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
        INSERT INTO dbo.F03Functions
            (FunctionCode, FunctionKey, FunctionName, Detail, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
        VALUES
            (3044, N'WorkCalendar.SymbolRuleManage', N'WorkCalendar.SymbolRuleManage', N'Quản lý quy tắc ký hiệu chấm công', N'WorkCalendar', N'SymbolRuleManage', N'All', N'Active', 1);
    ELSE
        INSERT INTO dbo.F03Functions
            (FunctionCode, FunctionName, Detail, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
        VALUES
            (3044, N'WorkCalendar.SymbolRuleManage', N'Quản lý quy tắc ký hiệu chấm công', N'WorkCalendar', N'SymbolRuleManage', N'All', N'Active', 1);
END
ELSE
BEGIN
    UPDATE dbo.F03Functions
    SET FunctionName = N'WorkCalendar.SymbolRuleManage',
        Detail = N'Quản lý quy tắc ký hiệu chấm công',
        ModuleCode = N'WorkCalendar',
        ActionCode = N'SymbolRuleManage',
        ScopeCode = N'All',
        LifecycleStatus = N'Active',
        IsActive = 1
    WHERE FunctionCode = 3044;

    IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
        UPDATE dbo.F03Functions
        SET FunctionKey = N'WorkCalendar.SymbolRuleManage'
        WHERE FunctionCode = 3044;
END;
GO

/* Symbol-rule management is module-wide. Seed the canonical SuperAdmin/Admin
   grants; Feature Operator Assignment still controls designated operators. */
INSERT INTO dbo.F03RoleFunctions
(
    IdRole, IdFunction, IsActive, CreatedBy, CreatedAt
)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode = 3044
  AND f.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );

UPDATE rf
SET rf.IsActive = 1
FROM dbo.F03RoleFunctions AS rf
INNER JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
INNER JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode = 3044
  AND f.IsActive = 1;
GO

IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.F03SecurityFunctionRegistry WHERE FunctionKey = N'WorkCalendar.SymbolRuleManage')
BEGIN
    INSERT INTO dbo.F03SecurityFunctionRegistry
    (
        FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
        LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
    )
    SELECT
        N'WorkCalendar.SymbolRuleManage', 3044, N'WorkCalendar.SymbolRuleManage',
        N'WorkCalendar', N'SymbolRuleManage', N'All', N'Active', N'SqlSeed',
        CONVERT(varchar(128), HASHBYTES('SHA2_256', N'WorkCalendar.SymbolRuleManage|3044|WorkCalendar.SymbolRuleManage'), 2),
        GETDATE(), GETDATE(), 0;
END;
GO

DECLARE @R TABLE(Code nvarchar(100),Name nvarchar(200),DayType nvarchar(40),ShiftCode nvarchar(50),Priority int,MinActual int NULL,InFrom varchar(5) NULL,CheckInToSeed varchar(5) NULL,JsonValue nvarchar(max));
INSERT INTO @R VALUES
(N'NORMAL-C1',N'Ngày thường - C1',N'NORMAL',N'C1',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"06:00","end":"14:00","symbolTemplate":"C1"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"K{hours}"}]}'),
(N'NORMAL-C2',N'Ngày thường - C2',N'NORMAL',N'C2',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"14:00","end":"22:00","symbolTemplate":"C2"},{"segmentType":"OT","start":"10:00","end":"14:00","symbolTemplate":"K{hours}"}]}'),
(N'NORMAL-C3',N'Ngày thường - C3',N'NORMAL',N'C3',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"22:00","end":"06:00","symbolTemplate":"C3"},{"segmentType":"OT","start":"18:00","end":"22:00","symbolTemplate":"K{hours}"}]}'),
(N'NORMAL-HC',N'Ngày thường - HC',N'NORMAL',N'HC',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"08:00","end":"16:45","symbolTemplate":"HC"},{"segmentType":"OT","start":"16:45","end":"19:45","symbolTemplate":"K{hours}"}]}'),
(N'NT-C1',N'Nghỉ công ty (T2-T7) - C1',N'COMPANY_HOLIDAY',N'C1',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"06:00","end":"14:00","symbolTemplate":"T{hours}"}]}'),
(N'NT-C2',N'Nghỉ công ty (T2-T7) - C2',N'COMPANY_HOLIDAY',N'C2',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"14:00","end":"22:00","symbolTemplate":"T{hours}"}]}'),
(N'NT-C3',N'Nghỉ công ty (T2-T7) - C3',N'COMPANY_HOLIDAY',N'C3',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"22:00","end":"06:00","symbolTemplate":"T{hours}"}]}'),
(N'CT-C1',N'Nghỉ Chủ nhật (CN) - C1',N'SUNDAY',N'C1',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"06:00","end":"14:00","symbolTemplate":"CN{hours}"}]}'),
(N'CT-C2',N'Nghỉ Chủ nhật (CN) - C2',N'SUNDAY',N'C2',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"14:00","end":"22:00","symbolTemplate":"CN{hours}"}]}'),
(N'CT-C3',N'Nghỉ Chủ nhật (CN) - C3',N'SUNDAY',N'C3',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"22:00","end":"06:00","symbolTemplate":"CN{hours}"}]}'),
(N'HOL-C1',N'Ngày lễ (L) - C1',N'NATIONAL_HOLIDAY',N'C1',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"06:00","end":"14:00","symbolTemplate":"NL{hours}"}]}'),
(N'HOL-C2',N'Ngày lễ (L) - C2',N'NATIONAL_HOLIDAY',N'C2',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"14:00","end":"22:00","symbolTemplate":"NL{hours}"}]}'),
(N'HOL-C3',N'Ngày lễ (L) - C3',N'NATIONAL_HOLIDAY',N'C3',100,NULL,NULL,NULL,N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"22:00","end":"06:00","symbolTemplate":"NL{hours}"}]}'),
(N'NT-KIP06-18',N'Nghỉ công ty (T2-T7) - Kíp 06-18',N'COMPANY_HOLIDAY',N'C1',10,705,'05:00','07:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"06:00","end":"14:00","symbolTemplate":"T{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"TK{hours}","fixedMinutes":240}]}'),
(N'NT-KIP10-22',N'Nghỉ công ty (T2-T7) - Kíp 10-22',N'COMPANY_HOLIDAY',N'C2',10,705,'09:00','11:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"10:00","end":"18:00","symbolTemplate":"T2{hours}"},{"segmentType":"OT","start":"18:00","end":"22:00","symbolTemplate":"TK{hours}","fixedMinutes":240}]}'),
(N'NT-KIP18-06',N'Nghỉ công ty (T2-T7) - Kíp 18-06',N'COMPANY_HOLIDAY',N'C3',10,705,'17:00','19:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"18:00","end":"06:00","symbolTemplate":"T3{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"TK{hours}","fixedMinutes":240}]}'),
(N'CT-KIP06-18',N'Nghỉ Chủ nhật (CN) - Kíp 06-18',N'SUNDAY',N'C1',10,705,'05:00','07:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"06:00","end":"14:00","symbolTemplate":"CN{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"CNK{hours}","fixedMinutes":240}]}'),
(N'CT-KIP10-22',N'Nghỉ Chủ nhật (CN) - Kíp 10-22',N'SUNDAY',N'C2',10,705,'09:00','11:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"10:00","end":"18:00","symbolTemplate":"CNC2{hours}"},{"segmentType":"OT","start":"18:00","end":"22:00","symbolTemplate":"CNK{hours}","fixedMinutes":240}]}'),
(N'CT-KIP18-06',N'Nghỉ Chủ nhật (CN) - Kíp 18-06',N'SUNDAY',N'C3',10,705,'17:00','19:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"18:00","end":"06:00","symbolTemplate":"CNC3{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"CNK{hours}","fixedMinutes":240}]}'),
(N'HOL-KIP06-18',N'Ngày lễ (L) - Kíp 06-18',N'NATIONAL_HOLIDAY',N'C1',10,705,'05:00','07:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"06:00","end":"14:00","symbolTemplate":"NL{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"NLK{hours}","fixedMinutes":240}]}'),
(N'HOL-KIP10-22',N'Ngày lễ (L) - Kíp 10-22',N'NATIONAL_HOLIDAY',N'C2',10,705,'09:00','11:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"10:00","end":"18:00","symbolTemplate":"NLC2{hours}"},{"segmentType":"OT","start":"18:00","end":"22:00","symbolTemplate":"NLK{hours}","fixedMinutes":240}]}'),
(N'HOL-KIP18-06',N'Ngày lễ (L) - Kíp 18-06',N'NATIONAL_HOLIDAY',N'C3',10,705,'17:00','19:00',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"FIXED_OT","fixedOtMinutes":240,"segments":[{"segmentType":"WORK","start":"18:00","end":"06:00","symbolTemplate":"NLC3{hours}"},{"segmentType":"OT","start":"14:00","end":"18:00","symbolTemplate":"NLK{hours}","fixedMinutes":240}]}');
INSERT INTO dbo.F03AttendanceSymbolRules(RuleCode,RuleName,DayType,ShiftCode,Priority,MinActualMinutes,CheckInFrom,CheckInTo,RuleJson,IsActive,CreatedBy,CreatedAt,LastModifiedSource)
SELECT Code,Name,DayType,ShiftCode,Priority,MinActual,InFrom,CheckInToSeed,JsonValue,1,0,SYSUTCDATETIME(),N'SEED'
FROM @R AS r
WHERE NOT EXISTS(SELECT 1 FROM dbo.F03AttendanceSymbolRules AS x WHERE x.RuleCode=r.Code);

/* Legacy/custom rest-day category from the reference table (DT symbols, 200% group). */
INSERT INTO dbo.F03AttendanceSymbolRules
    (RuleCode,RuleName,DayType,ShiftCode,Priority,RuleJson,IsActive,CreatedBy,CreatedAt,LastModifiedSource)
SELECT v.RuleCode,v.RuleName,N'OTHER',v.ShiftCode,100,v.RuleJson,1,0,SYSUTCDATETIME(),N'SEED'
FROM (VALUES
 (N'OTHER-C1',N'Nghỉ khác - C1',N'C1',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"06:00","end":"18:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C2',N'Nghỉ khác - C2',N'C2',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"10:00","end":"22:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C3',N'Nghỉ khác - C3',N'C3',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"OT","start":"18:00","end":"06:00","symbolTemplate":"DT{hours}"}]}' )
) AS v(RuleCode,RuleName,ShiftCode,RuleJson)
WHERE NOT EXISTS(SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.RuleCode=v.RuleCode);

/* Reclassify seeded rules already present in the database.
   HRM LNLMa=0001 is NORMAL and must not produce holiday rules.
   HRM LNLMa=0002 is the shared Sunday/company-rest catalog:
   NNLLoai=2 + Mon-Sat -> COMPANY_HOLIDAY (NT/T/TK rules);
   NNLLoai=2 + Sunday -> SUNDAY (CT/CN/CNK rules).
   HRM NNLLoai=1 -> LNLMa=0003 -> NATIONAL_HOLIDAY (HOL/NL rules). */
UPDATE dbo.F03AttendanceSymbolRules
   SET DayType = N'COMPANY_HOLIDAY',
       RuleName = REPLACE(RuleName, N'Ngày nghỉ thường (NT)', N'Nghỉ công ty (T2-T7)')
 WHERE RuleCode LIKE N'NT-%';
UPDATE dbo.F03AttendanceSymbolRules
   SET DayType = N'SUNDAY',
       RuleName = REPLACE(REPLACE(RuleName, N'Chủ nhật / Nghỉ công ty (CT)', N'Nghỉ Chủ nhật (CN)'), N'Chủ nhật / Nghỉ công ty', N'Nghỉ Chủ nhật')
 WHERE RuleCode LIKE N'CT-%';

GO

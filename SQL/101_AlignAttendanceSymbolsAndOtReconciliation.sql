/*
    Align the persisted attendance/OT symbol rules with Bang_Quy_Dinh_OT.xlsx.
    HRM day type codes: 001=COMPANY_HOLIDAY (T/TK), 002=SUNDAY (CN/CNK),
    003=NATIONAL_HOLIDAY (NL/NLK). NORMAL uses K, OTHER uses DT.
    This migration is idempotent; run it after SQL/67_AttendanceSymbolRules.sql.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.F03AttendanceSymbolRules',N'U') IS NULL
    THROW 53101, N'Run SQL/67_AttendanceSymbolRules.sql before this migration.', 1;

/* General OT on company rest days uses T1..T11.5. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'T{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode IN (N'NT-C1',N'NT-C2',N'NT-C3')
  AND ISJSON(RuleJson)=1;

/* General OT on Sundays uses CN1..CN11.5. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'CN{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode IN (N'CT-C1',N'CT-C2',N'CT-C3')
  AND ISJSON(RuleJson)=1;

/* General OT on national holidays uses NL1..NL14. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'NL{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode IN (N'HOL-C1',N'HOL-C2',N'HOL-C3')
  AND ISJSON(RuleJson)=1;

/* K-family shift templates: preserve the exact combined symbols in the sheet,
   e.g. T28/T38, CNC28/CNC38, NLC28/NLC38. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'T2{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'NT-KIP10-22' AND ISJSON(RuleJson)=1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'T3{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'NT-KIP18-06' AND ISJSON(RuleJson)=1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'CNC2{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'CT-KIP10-22' AND ISJSON(RuleJson)=1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'CNC3{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'CT-KIP18-06' AND ISJSON(RuleJson)=1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'NLC2{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'HOL-KIP10-22' AND ISJSON(RuleJson)=1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[0].symbolTemplate',N'NLC3{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode=N'HOL-KIP18-06' AND ISJSON(RuleJson)=1;

/* Keep the separate fixed-OT families exactly as defined by the workbook. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[1].symbolTemplate',N'TK{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode LIKE N'NT-KIP%' AND ISJSON(RuleJson)=1
  AND JSON_QUERY(RuleJson,'$.segments[1]') IS NOT NULL;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[1].symbolTemplate',N'CNK{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode LIKE N'CT-KIP%' AND ISJSON(RuleJson)=1
  AND JSON_QUERY(RuleJson,'$.segments[1]') IS NOT NULL;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson=JSON_MODIFY(RuleJson,'$.segments[1].symbolTemplate',N'NLK{hours}'),
    ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'OT_REFERENCE_XLSX'
WHERE RuleCode LIKE N'HOL-KIP%' AND ISJSON(RuleJson)=1
  AND JSON_QUERY(RuleJson,'$.segments[1]') IS NOT NULL;

/* DT3..DT11 is a separate 200% family for OTHER/rest-day records. */
INSERT INTO dbo.F03AttendanceSymbolRules
    (RuleCode,RuleName,DayType,ShiftCode,Priority,RuleJson,IsActive,CreatedBy,CreatedAt,LastModifiedSource)
SELECT v.RuleCode,v.RuleName,N'OTHER',v.ShiftCode,100,v.RuleJson,1,0,SYSUTCDATETIME(),N'OT_REFERENCE_XLSX'
FROM (VALUES
 (N'OTHER-C1',N'Loại khác - C1',N'C1',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"06:00","end":"14:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C2',N'Loại khác - C2',N'C2',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"14:00","end":"22:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C3',N'Loại khác - C3',N'C3',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"22:00","end":"06:00","symbolTemplate":"DT{hours}"}]}')
) AS v(RuleCode,RuleName,ShiftCode,RuleJson)
WHERE NOT EXISTS(SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.RuleCode=v.RuleCode);

COMMIT TRANSACTION;

SELECT RuleCode,DayType,ShiftCode,IsActive,RuleJson
FROM dbo.F03AttendanceSymbolRules
WHERE RuleCode LIKE N'NT-%' OR RuleCode LIKE N'CT-%' OR RuleCode LIKE N'HOL-%' OR RuleCode LIKE N'OTHER-%'
ORDER BY DayType,ShiftCode,Priority,RuleCode;

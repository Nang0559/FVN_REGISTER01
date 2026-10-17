/*
    Align persisted HRM attendance-symbol templates with the approved reference table.
    Symbol codes such as T2, T3, CNC2, CNC3, NLC2 and NLC3 are literal symbols;
    the corresponding hours live in the adjacent hours column and must not be
    appended to the symbol itself. OT-segment symbols use TK/CNK/NLK consistently.
    Idempotent: safe to execute more than once.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'T2'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'NT-C2', N'NT-KIP10-22')
  AND ISJSON(RuleJson) = 1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'T3'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'NT-C3', N'NT-KIP18-06')
  AND ISJSON(RuleJson) = 1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'CNC2'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'CT-C2', N'CT-KIP10-22')
  AND ISJSON(RuleJson) = 1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'CNC3'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'CT-C3', N'CT-KIP18-06')
  AND ISJSON(RuleJson) = 1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'NLC2'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'HOL-C2', N'HOL-KIP10-22')
  AND ISJSON(RuleJson) = 1;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[0].symbolTemplate', N'NLC3'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode IN (N'HOL-C3', N'HOL-KIP18-06')
  AND ISJSON(RuleJson) = 1;

/* For the 10-22 shift the OT segment must use the same K-family as the reference. */
UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[1].symbolTemplate', N'TK{hours}'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode = N'NT-KIP10-22'
  AND ISJSON(RuleJson) = 1
  AND JSON_QUERY(RuleJson, '$.segments[1]') IS NOT NULL;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[1].symbolTemplate', N'CNK{hours}'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode = N'CT-KIP10-22'
  AND ISJSON(RuleJson) = 1
  AND JSON_QUERY(RuleJson, '$.segments[1]') IS NOT NULL;

UPDATE dbo.F03AttendanceSymbolRules
SET RuleJson = JSON_MODIFY(RuleJson, '$.segments[1].symbolTemplate', N'NLK{hours}'),
    ModifiedAt = SYSUTCDATETIME(),
    LastModifiedSource = N'HRM_SYMBOL_ALIGNMENT'
WHERE RuleCode = N'HOL-KIP10-22'
  AND ISJSON(RuleJson) = 1
  AND JSON_QUERY(RuleJson, '$.segments[1]') IS NOT NULL;

/* Ensure the DT symbol family from the reference table is available for legacy/custom rest days. */
INSERT INTO dbo.F03AttendanceSymbolRules
    (RuleCode,RuleName,DayType,ShiftCode,Priority,RuleJson,IsActive,CreatedBy,CreatedAt,LastModifiedSource)
SELECT v.RuleCode,v.RuleName,N'OTHER',v.ShiftCode,100,v.RuleJson,1,0,SYSUTCDATETIME(),N'HRM_SYMBOL_ALIGNMENT'
FROM (VALUES
 (N'OTHER-C1',N'Nghỉ khác - C1',N'C1',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"06:00","end":"18:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C2',N'Nghỉ khác - C2',N'C2',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"10:00","end":"22:00","symbolTemplate":"DT{hours}"}]}'),
 (N'OTHER-C3',N'Nghỉ khác - C3',N'C3',N'{"blockMinutes":15,"roundingMode":"FLOOR","allocationMode":"STANDARD","segments":[{"segmentType":"WORK","start":"18:00","end":"06:00","symbolTemplate":"DT{hours}"}]}' )
) AS v(RuleCode,RuleName,ShiftCode,RuleJson)
WHERE NOT EXISTS(SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.RuleCode=v.RuleCode);

COMMIT TRANSACTION;

SELECT RuleCode, DayType, ShiftCode, IsActive, RuleJson
FROM dbo.F03AttendanceSymbolRules
WHERE RuleCode IN
(
    N'NT-C2',N'NT-C3',N'NT-KIP10-22',N'NT-KIP18-06',
    N'CT-C2',N'CT-C3',N'CT-KIP10-22',N'CT-KIP18-06',
    N'HOL-C2',N'HOL-C3',N'HOL-KIP10-22',N'HOL-KIP18-06'
)
ORDER BY RuleCode;

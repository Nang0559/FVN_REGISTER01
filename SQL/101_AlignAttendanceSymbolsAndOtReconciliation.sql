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

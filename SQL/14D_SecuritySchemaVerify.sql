/* Verify the authorization schema required by runtime services. */
SET NOCOUNT ON;
DECLARE @Missing TABLE(ObjectName sysname NOT NULL);
IF OBJECT_ID(N'dbo.F03Users',N'U') IS NULL INSERT INTO @Missing VALUES(N'F03Users');
IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NULL INSERT INTO @Missing VALUES(N'F03Functions');
IF OBJECT_ID(N'dbo.F03UserFunctions',N'U') IS NULL INSERT INTO @Missing VALUES(N'F03UserFunctions');
IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NULL INSERT INTO @Missing VALUES(N'F03ManagedScopes');
IF OBJECT_ID(N'dbo.F03FeatureOperatorAssignments',N'U') IS NULL INSERT INTO @Missing VALUES(N'F03FeatureOperatorAssignments');
IF EXISTS(SELECT 1 FROM @Missing)
BEGIN
    SELECT ObjectName AS MissingObject FROM @Missing ORDER BY ObjectName;
    THROW 51014,'Required authorization schema is incomplete.',1;
END;

/* Runtime model contract for F03FeatureOperatorAssignments. */
DECLARE @MissingColumn TABLE(ColumnName sysname NOT NULL);
DECLARE @AssignmentObjectId int = OBJECT_ID(N'dbo.F03FeatureOperatorAssignments',N'U');
INSERT INTO @MissingColumn(ColumnName)
SELECT v.ColumnName
FROM (VALUES
    (N'EmployeeCode'),
    (N'FunctionCode'),
    (N'ResourceType'),
    (N'ResourceId'),
    (N'ScopeCode'),
    (N'Remark'),
    (N'IsActive'),
    (N'CreatedBy'),
    (N'CreatedAt'),
    (N'ModifiedBy'),
    (N'ModifiedAt'),
    (N'LastModifiedSource')
) v(ColumnName)
WHERE COL_LENGTH(N'dbo.F03FeatureOperatorAssignments', v.ColumnName) IS NULL;

IF EXISTS(SELECT 1 FROM @MissingColumn)
BEGIN
    SELECT ColumnName AS MissingColumn FROM @MissingColumn ORDER BY ColumnName;
    THROW 51015,'F03FeatureOperatorAssignments schema does not match the runtime model.',1;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = @AssignmentObjectId
      AND name = N'ScopeCode'
      AND (system_type_id <> TYPE_ID(N'nvarchar') OR max_length <> 60)
)
    THROW 51016,'F03FeatureOperatorAssignments.ScopeCode must be nvarchar(30).',1;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = @AssignmentObjectId
      AND name = N'Remark'
      AND (system_type_id <> TYPE_ID(N'nvarchar') OR max_length < 2000)
)
    THROW 51017,'F03FeatureOperatorAssignments.Remark must support nvarchar(1000).',1;

PRINT N'Authorization schema verification passed.';
GO

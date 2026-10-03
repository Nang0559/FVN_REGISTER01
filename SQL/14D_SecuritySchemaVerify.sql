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
PRINT N'Authorization schema verification passed.';
GO

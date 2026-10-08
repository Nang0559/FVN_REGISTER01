/*
===============================================================================
 76 - Approval policy seed: 4-level ladder for every request type

 Ladder (edit the @Ladder table below if your HRM position codes differ):
   Level 1  Sub-Leader / Leader
   Level 2  Chief / Ast.Chief
   Level 3  AMG (Ast.Manager)  /  MG (Manager, Sen.Manager)
   Level 4  GM

 Who approves a request depends on the REQUESTER's position (a request goes only UP the ladder):
   Worker / Staff / Nhân viên / ... (non-approvers) -> L1, L2, L3 (AMG+MG), L4
   Sub-Leader / Leader                              -> L2, L3 (AMG+MG), L4
   Chief / Ast.Chief                                -> L3 (AMG+MG), L4
   AMG (Ast.Manager)                                -> L3 (MG only), L4
   MG (Manager / Sen.Manager)                       -> L4
   GM                                               -> no policy (nobody above)

 Several positions can share one level (e.g. Sub-Leader and Leader at level 1): one policy row
 per approval position; the approval route offers all of them as candidates for that level.
 Requires the unique index of SQL/36 with ApprovalPositionCode in its key (rebuilt below if old).

 Scope : RequestType 0 Leave, 1 Overtime, 2 Trip, 3 Equipment (the types usp_ReconcileEmployeeApprovers
         can turn into F03Approvers) x every active department.
 Safe  : by default a (RequestType, Department) that already has ACTIVE policies is left untouched.
         Set @Overwrite = 1 to deactivate those and replace them with the ladder.
 Next  : run the HRM sync (security provisioning) so F03Approvers are created from these policies.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* The unique index must allow several approval positions per level. */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03ApprovalPolicies_Request_Dept_Position_Level'
             AND object_id=OBJECT_ID(N'dbo.F03ApprovalPolicies'))
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE i.object_id=OBJECT_ID(N'dbo.F03ApprovalPolicies')
      AND i.name=N'UX_F03ApprovalPolicies_Request_Dept_Position_Level'
      AND c.name=N'ApprovalPositionCode'
)
    DROP INDEX UX_F03ApprovalPolicies_Request_Dept_Position_Level ON dbo.F03ApprovalPolicies;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03ApprovalPolicies_Request_Dept_Position_Level'
                 AND object_id=OBJECT_ID(N'dbo.F03ApprovalPolicies'))
    CREATE UNIQUE INDEX UX_F03ApprovalPolicies_Request_Dept_Position_Level
        ON dbo.F03ApprovalPolicies(RequestType,DeptCode,PositionCode,Level,ApprovalPositionCode);
GO

DECLARE @OnlyDeptCode int = NULL;  -- NULL = all active departments, or a single DeptCode
DECLARE @Overwrite    bit = 0;     -- 0 = skip (RequestType, Dept) that already has active policies
DECLARE @CreatedBy    int = 0;

/* Approval ladder. Tier = rank used for "only UP the ladder" (Tier 5 = top, never needs approval). */
DECLARE @Ladder TABLE
(
    PositionCode  nvarchar(20) NOT NULL PRIMARY KEY,
    ApprovalLevel int          NOT NULL,
    Tier          int          NOT NULL,
    RoleName      nvarchar(50) NOT NULL
);
INSERT @Ladder(PositionCode,ApprovalLevel,Tier,RoleName) VALUES
 (N'0002',1,1,N'SubLeader'),  -- Sub- Leader
 (N'0006',1,1,N'SubLeader'),  -- Leader
 (N'0004',2,2,N'Chief'),      -- Chief
 (N'0010',2,2,N'Chief'),      -- Ast.chief
 (N'0011',3,3,N'Manager'),    -- Ast.Manager  (AMG)
 (N'0005',3,4,N'Manager'),    -- Manager      (MG)
 (N'0009',3,4,N'Manager'),    -- Sen.Manager  (treated as MG)
 (N'0001',4,5,N'GM');         -- Giám đốc     (GM)

DECLARE @Missing nvarchar(max) =
(
    SELECT STRING_AGG(l.PositionCode,N', ')
    FROM @Ladder l
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Positions p WHERE p.PositionCode=l.PositionCode AND p.IsActive=1)
);
IF @Missing IS NOT NULL
    PRINT N'WARNING: position(s) not found or inactive in F03Positions, skipped: '+@Missing;

DELETE l FROM @Ladder l
WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Positions p WHERE p.PositionCode=l.PositionCode AND p.IsActive=1);

IF EXISTS (SELECT 1 FROM (VALUES (1),(2),(3),(4)) v(Lvl)
           WHERE NOT EXISTS (SELECT 1 FROM @Ladder l WHERE l.ApprovalLevel=v.Lvl))
    THROW 51410,'The approval ladder has no active position for at least one level (1-4). Fix the position codes in @Ladder.',1;

/* Requesters: ladder positions below the top + every other active position that has active employees. */
DECLARE @Requester TABLE (PositionCode nvarchar(20) NOT NULL PRIMARY KEY, Tier int NOT NULL);
INSERT @Requester(PositionCode,Tier)
SELECT PositionCode,Tier FROM @Ladder WHERE Tier<5;

INSERT @Requester(PositionCode,Tier)
SELECT p.PositionCode,0
FROM dbo.F03Positions p
WHERE p.IsActive=1
  AND NOT EXISTS (SELECT 1 FROM @Ladder l WHERE l.PositionCode=p.PositionCode)
  AND EXISTS (SELECT 1 FROM dbo.F03Employees e
              WHERE e.IsActive=1 AND LTRIM(RTRIM(e.PositionCode))=p.PositionCode);

DECLARE @Types TABLE (RequestType int NOT NULL PRIMARY KEY);
INSERT @Types(RequestType) VALUES (0),(1),(2),(3);   -- Leave, Overtime, Trip, Equipment

DECLARE @Scope TABLE (RequestType int NOT NULL, DeptCode int NOT NULL, PRIMARY KEY(RequestType,DeptCode));
INSERT @Scope(RequestType,DeptCode)
SELECT t.RequestType,d.DeptCode
FROM @Types t
CROSS JOIN dbo.F03Departments d
WHERE d.IsActive=1 AND d.DeptCode>0
  AND (@OnlyDeptCode IS NULL OR d.DeptCode=@OnlyDeptCode)
  AND (@Overwrite=1
       OR NOT EXISTS (SELECT 1 FROM dbo.F03ApprovalPolicies ap
                      WHERE ap.RequestType=t.RequestType AND ap.DeptCode=d.DeptCode AND ap.IsActive=1));

BEGIN TRANSACTION;

IF @Overwrite=1
    UPDATE ap
       SET ap.IsActive=0, ap.ModifiedBy=@CreatedBy, ap.ModifiedAt=GETDATE(), ap.LastModifiedSource=N'Seed'
    FROM dbo.F03ApprovalPolicies ap
    JOIN @Scope s ON s.RequestType=ap.RequestType AND s.DeptCode=ap.DeptCode
    WHERE ap.IsActive=1;

SELECT s.RequestType,s.DeptCode,
       r.PositionCode AS RequesterPosition,
       a.PositionCode AS ApprovalPosition,
       a.ApprovalLevel AS Lvl,
       a.RoleName,
       LEFT(p.PositionName,100) AS PositionName
INTO #Desired
FROM @Scope s
CROSS JOIN @Requester r
JOIN @Ladder a ON a.Tier>r.Tier
JOIN dbo.F03Positions p ON p.PositionCode=a.PositionCode;

/* Re-activate rows that already exist (the unique index counts inactive rows too). */
UPDATE ap
   SET ap.IsActive=1, ap.Sequence=d.Lvl, ap.LevelName=d.PositionName, ap.RoleName=d.RoleName,
       ap.Required=1, ap.ModifiedBy=@CreatedBy, ap.ModifiedAt=GETDATE(), ap.LastModifiedSource=N'Seed'
FROM dbo.F03ApprovalPolicies ap
JOIN #Desired d
  ON ap.RequestType=d.RequestType AND ap.DeptCode=d.DeptCode AND ap.PositionCode=d.RequesterPosition
 AND ap.Level=d.Lvl AND ap.ApprovalPositionCode=d.ApprovalPosition
WHERE ISNULL(ap.IsActive,0)=0;

INSERT dbo.F03ApprovalPolicies
    (IsActive,CreatedBy,LastModifiedSource,CreatedAt,
     RequestType,DeptCode,PositionCode,ApprovalPositionCode,Level,Sequence,LevelName,RoleName,Required)
SELECT 1,@CreatedBy,N'Seed',GETDATE(),
       d.RequestType,d.DeptCode,d.RequesterPosition,d.ApprovalPosition,d.Lvl,d.Lvl,d.PositionName,d.RoleName,1
FROM #Desired d
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.F03ApprovalPolicies ap
    WHERE ap.RequestType=d.RequestType AND ap.DeptCode=d.DeptCode AND ap.PositionCode=d.RequesterPosition
      AND ap.Level=d.Lvl AND ap.ApprovalPositionCode=d.ApprovalPosition
);

COMMIT TRANSACTION;

/* Result 1: what was seeded. */
SELECT RequestType,
       Departments=COUNT(DISTINCT DeptCode),
       PolicyRows=COUNT(*)
FROM dbo.F03ApprovalPolicies
WHERE IsActive=1 AND LastModifiedSource=N'Seed'
GROUP BY RequestType ORDER BY RequestType;

/* Result 2: the ladder as one department will see it (requester position -> approvers per level). */
SELECT MIN(DeptCode) AS DeptCode INTO #FirstDept FROM #Desired;

SELECT rp.PositionName AS RequesterPosition,
       d.Lvl AS Level,
       Approvers=STRING_AGG(ap.PositionName,N' / ')
FROM (SELECT DISTINCT d.RequesterPosition,d.Lvl,d.ApprovalPosition,d.RequestType,d.DeptCode FROM #Desired d) d
JOIN #FirstDept f ON f.DeptCode=d.DeptCode
JOIN dbo.F03Positions rp ON rp.PositionCode=d.RequesterPosition
JOIN dbo.F03Positions ap ON ap.PositionCode=d.ApprovalPosition
WHERE d.RequestType=1
GROUP BY rp.PositionName,d.RequesterPosition,d.Lvl
ORDER BY d.RequesterPosition,d.Lvl;

DROP TABLE #FirstDept;
DROP TABLE #Desired;
GO

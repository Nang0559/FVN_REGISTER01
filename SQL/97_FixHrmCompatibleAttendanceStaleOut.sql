/*
  Fix stale OUT records leaking from the previous attendance day.

  Context:
    SQL/22_02_HrmCompatibleTimeKeepingForStaff.sql already contains the
    multi-night candidate-shift scan introduced in commit 5234d2f0.
    This migration keeps that logic intact and adds the missing guard for:
      1) a selected shift whose OUT is earlier than its IN;
      2) an OUT with no IN on the current day which actually belongs to a
         previous shift.

  The patch is applied to the deployed procedure definition so this file is
  safe to run against databases where the procedure already exists.
*/
SET NOCOUNT ON;

DECLARE @ObjectId int = OBJECT_ID(N'dbo.usp_HrmCompatibleTimeKeepingForStaff', N'P');
DECLARE @Definition nvarchar(max);
DECLARE @Marker nvarchar(max);
DECLARE @Patch nvarchar(max);

IF @ObjectId IS NULL
    THROW 50001, 'dbo.usp_HrmCompatibleTimeKeepingForStaff does not exist. Deploy SQL/22_02_HrmCompatibleTimeKeepingForStaff.sql first.', 1;

SET @Definition = OBJECT_DEFINITION(@ObjectId);

IF @Definition IS NULL
    THROW 50002, 'Cannot read the definition of dbo.usp_HrmCompatibleTimeKeepingForStaff.', 1;

SET @Marker = N'\t\t-- Neu chua co ca, chi ket thuc phan tinh Work/OT; IN/OUT raw van duoc\n\t\t-- ghi ra #tblBaoCao de co the kiem tra truc tiep RecordDataNew.';

SET @Patch = N'
		/*
		  FIX STALE OUT: do not let an OUT belonging to the previous day/shift
		  become the OUT of the current shift.

		  (A) A selected shift has IN but OUT < IN: the OUT is from an earlier
		      shift and must not be used for the current shift.
		  (B) There is no IN for the current day but there is an OUT: when the
		      OUT is paired with a previous IN within the same 20-hour window
		      and that previous IN has no later OUT before the current OUT, the
		      OUT belongs to the previous shift and must be discarded.
		*/
		IF @Maca > 0
		   AND @TGDen > ''19000101''
		   AND @TGVe > ''19000101''
		   AND @TGVe < @TGDen
		BEGIN
			SELECT
				@TGVe=''1900-01-01 00:00:00.000'',
				@CuaVe=0;
		END;

		IF @TGDen <= ''19000101''
		   AND @TGVe > ''19000101''
		BEGIN
			DECLARE @PrevInTime datetime = NULL;

			SELECT TOP (1)
				@PrevInTime=R.ThoiGian
			FROM HRM.dbo.RecordDataNew R
			INNER JOIN HRM.dbo.tblCapThe C
				ON C.CTMaNV=@StaffID
			   AND C.CTMaThe=R.IDCard
			LEFT JOIN HRM.dbo.tblDauDoc DD
				ON DD.DDMa=R.IDM
			WHERE ISNULL(DD.DDloaiChamCong,N'''')=N''''
			  AND DD.DDChinhVao=1
			  AND R.ThoiGian >= DATEADD(minute,-1200,@TGVe)
			  AND R.ThoiGian < @TGVe
			  AND NOT EXISTS
			  (
				  SELECT 1
				  FROM HRM.dbo.RecordDataNew R2
				  INNER JOIN HRM.dbo.tblCapThe C2
					  ON C2.CTMaNV=@StaffID
					 AND C2.CTMaThe=R2.IDCard
				  LEFT JOIN HRM.dbo.tblDauDoc DD2
					  ON DD2.DDMa=R2.IDM
				  WHERE ISNULL(DD2.DDloaiChamCong,N'''')=N''''
					AND DD2.DDChinhVao=0
					AND R2.ThoiGian > R.ThoiGian
					AND R2.ThoiGian < @TGVe
			  )
			ORDER BY R.ThoiGian DESC;

			IF @PrevInTime IS NOT NULL
			BEGIN
				SELECT
					@TGVe=''1900-01-01 00:00:00.000'',
					@CuaVe=0;
			END
		END;

';

IF CHARINDEX(N'FIX STALE OUT:', @Definition) > 0
BEGIN
    PRINT N'SKIP: stale-OUT fix is already present in dbo.usp_HrmCompatibleTimeKeepingForStaff.';
    RETURN;
END;

IF CHARINDEX(@Marker, @Definition) = 0
    THROW 50003, 'Patch marker was not found in dbo.usp_HrmCompatibleTimeKeepingForStaff. The procedure version is not compatible with this migration.', 1;

SET @Definition = REPLACE(@Definition, @Marker, @Patch + @Marker);

EXEC sys.sp_executesql @Definition;

PRINT N'OK: stale-OUT fix applied to dbo.usp_HrmCompatibleTimeKeepingForStaff.';

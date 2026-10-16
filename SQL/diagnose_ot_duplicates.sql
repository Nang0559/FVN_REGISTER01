/* Existing duplicate OT registrations (same employee, same day, more than one live request). Read-only. */
SELECT  CONVERT(date, r.OTDate) AS OTDate,
        e.EmployeeCode,
        Requests    = COUNT(*),
        FirstReqId  = MIN(r.Id),
        LastReqId   = MAX(r.Id)
FROM dbo.F03OTEmployees e
JOIN dbo.F03OTRequests  r ON r.Id = e.OTRequestId
WHERE e.IsActive = 1
  AND r.IsActive = 1
  AND r.RequestStatus NOT IN (4, 5)      -- 4 Rejected, 5 Cancelled
GROUP BY CONVERT(date, r.OTDate), e.EmployeeCode
HAVING COUNT(*) > 1
ORDER BY OTDate DESC, e.EmployeeCode;

/* Details of those requests (who created each one, status, hours). */
SELECT  CONVERT(date, r.OTDate) AS OTDate, e.EmployeeCode, r.Id AS RequestId, r.OTCode,
        r.RequestStatus, CreatedByEmployee = r.EmployeeCode, r.CreatedAt, e.OTHours
FROM dbo.F03OTEmployees e
JOIN dbo.F03OTRequests  r ON r.Id = e.OTRequestId
JOIN (SELECT CONVERT(date, r2.OTDate) AS D, e2.EmployeeCode AS C
      FROM dbo.F03OTEmployees e2 JOIN dbo.F03OTRequests r2 ON r2.Id = e2.OTRequestId
      WHERE e2.IsActive = 1 AND r2.IsActive = 1 AND r2.RequestStatus NOT IN (4, 5)
      GROUP BY CONVERT(date, r2.OTDate), e2.EmployeeCode HAVING COUNT(*) > 1) d
  ON d.D = CONVERT(date, r.OTDate) AND d.C = e.EmployeeCode
WHERE e.IsActive = 1 AND r.IsActive = 1 AND r.RequestStatus NOT IN (4, 5)
ORDER BY OTDate DESC, e.EmployeeCode, r.Id;

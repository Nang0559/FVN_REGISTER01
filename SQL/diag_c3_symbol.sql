-- Chẩn đoán: vì sao ca C3 ngày thường không ra ký hiệu C3 (chạy trên DB FVN_REGISTER)
-- 1) Rule C3 đang bật: ShiftCode phải khớp ShiftAbbr bên dưới; kiểm tra Min/Max phút, khung giờ vào, hiệu lực
SELECT Id,RuleCode,DayType,ShiftCode,ShiftId,Priority,MinActualMinutes,MaxActualMinutes,CheckInFrom,CheckInTo,EffectiveFrom,EffectiveTo,IsActive,RuleJson
FROM dbo.F03AttendanceSymbolRules WHERE ShiftCode LIKE N'%3%' OR ShiftId IS NOT NULL ORDER BY Priority,Id;

-- 2) Dòng công C3 thực tế (đổi @code): xem ShiftAbbr, DayType, In/Out (Out có nhỏ hơn In không?), ký hiệu đã ghi
DECLARE @code nvarchar(50)=N'<MÃ NHÂN VIÊN>';
SELECT WorkDate,ShiftId,ShiftAbbr,DayType,CheckInTime,CheckOutTime,
       DATEDIFF(minute,CheckInTime,CheckOutTime) AS ElapsedMin,
       WorkMinutesDay,WorkMinutesNight,RequiredMinutes,WorkSymbol,OtSymbol,SymbolRuleCode,AttendanceDisplayValue
FROM dbo.F03HrmAttendanceCalculated
WHERE EmployeeCode=@code AND WorkDate>='2026-09-01' AND ShiftAbbr LIKE N'%3%'
UNION ALL
SELECT WorkDate,ShiftId,ShiftAbbr,DayType,CheckInTime,CheckOutTime,
       DATEDIFF(minute,CheckInTime,CheckOutTime),WorkMinutesDay,WorkMinutesNight,RequiredMinutes,WorkSymbol,OtSymbol,SymbolRuleCode,AttendanceDisplayValue
FROM dbo.F03HrmAttendanceHistory
WHERE EmployeeCode=@code AND WorkDate>='2026-09-01' AND ShiftAbbr LIKE N'%3%'
ORDER BY WorkDate;

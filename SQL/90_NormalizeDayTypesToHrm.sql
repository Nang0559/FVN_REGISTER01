/*
===============================================================================
90 - DEPRECATED / BLOCKED — KHÔNG CHẠY TRÊN DAYTYPE CONTRACT HIỆN TẠI
===============================================================================
Migration cũ này gộp SUNDAY vào COMPANY_HOLIDAY và đổi COMPANY_SATURDAY sang
OTHER. Điều đó trái với mapping canonical được SQL/84 và Calendar runtime sử dụng:

  LNLMa=0001                         -> NORMAL
  NNLLoai=2 -> LNLMa=0002 + Sunday  -> SUNDAY
  NNLLoai=2 -> LNLMa=0002 + Mon-Sat -> COMPANY_HOLIDAY
  NNLLoai=1 -> LNLMa=0003            -> NATIONAL_HOLIDAY

Không chạy phần migration cũ bên dưới. Script được thay bằng guard để tránh
vô tình làm sai DayType, ký hiệu chấm công và màu lịch trên database.
Nếu database đã từng chạy bản SQL/90 cũ, hãy kiểm tra dữ liệu trước; cài lại
SQL/84 không tự khôi phục các DayType/symbol đã bị migration cũ ghi đè.
===============================================================================
*/
USE [FVN_REGISTER];
GO
THROW 51990, N'SQL/90 đã bị vô hiệu hóa: migration này không tương thích với DayType mapping hiện tại. Dùng SQL/84 và tài liệu MÔ HÌNH/01_KIEN_TRUC_HE_THONG/24_WORK_CALENDAR_AND_ACTION_IMPLEMENTATION.md.', 1;
GO

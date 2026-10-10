namespace FVN_REGISTER.Contract.Dtos.Execution;

/// <summary>
/// File đính kèm của evidence, trả về cho HR xem/tải. Nội dung được mã hóa Base64
/// (giới hạn upload 10 MB) để đi qua ApiResponse JSON như các endpoint khác.
/// </summary>
public sealed record ExecutionEvidenceFileDto(
    string FileName,
    string ContentType,
    string ContentBase64);

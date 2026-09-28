namespace FVN_REGISTER.Contract.Dtos.Equipment;

/// <summary>
/// Thông tin tóm tắt của một Equipment Schema.
/// Đây là DTO chuẩn duy nhất cho danh sách Schema; Import Excel chỉ sử dụng DTO này,
/// không sở hữu một bản EquipmentSchemaSummaryDto riêng.
/// Quyền thực tế vẫn phải được kiểm tra ở server; các cờ Can* chỉ hỗ trợ UI.
/// </summary>
public sealed class EquipmentSchemaSummaryDto
{
    public int Id { get; init; }

    // Chuẩn mã + tên phòng ban dùng xuyên suốt hệ thống.
    public string DepartmentCode { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;
    public string SchemaKind { get; init; } = "Equipment";
    public string SchemaKey { get; init; } = string.Empty;
    public int Version { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int FieldCount { get; init; }

    public int CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int? UpdatedByUserId { get; init; }
    public string? UpdatedByUserName { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public int? SourceSchemaId { get; init; }
    public string? SourceFileName { get; init; }
    public bool CreatedFromExcel { get; init; }

    public bool IsOwner { get; init; }
    public bool CanEdit { get; init; }
    public bool CanCreateVersion { get; init; }
    public bool CanClone { get; init; }

    // Compatibility aliases for callers that still use the old import vocabulary.
    // New code should use DepartmentCode/CreatedByUserId/CreatedByUserName.
    public string DeptCode => DepartmentCode;
    public int OwnerUserId => CreatedByUserId;
    public string OwnerName => CreatedByUserName;
}

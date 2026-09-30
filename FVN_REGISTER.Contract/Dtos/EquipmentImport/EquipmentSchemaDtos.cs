namespace FVN_REGISTER.Contract.Dtos.EquipmentImport;

public sealed class EquipmentSchemaDto
{
    public int Id { get; set; }
    public string DeptCode { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string SchemaKind { get; set; } = "Equipment";
    public string SchemaKey { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int? SourceSchemaId { get; set; }
    public string? SourceFileName { get; set; }
    public bool CreatedFromExcel { get; set; }
    public bool IsOwner { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCreateVersion { get; set; }
    public bool CanClone { get; set; } = true;
    public List<EquipmentFieldDefinitionDto> Fields { get; set; } = new();
}

public sealed class EquipmentSchemaUpsertRequest
{
    public int? Id { get; set; }
    public string DeptCode { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string SchemaKind { get; set; } = "Equipment";
    public string Status { get; set; } = "Draft";
}

public sealed class EquipmentSchemaCloneRequest
{
    public string? SchemaName { get; set; }
    public string Status { get; set; } = "Draft";
}

public sealed class EquipmentSchemaFromExcelDto
{
    public string FileName { get; set; } = string.Empty;
    public string SuggestedSchemaName { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
    public int SampleRowCount { get; set; }
    public List<EquipmentFieldDefinitionDto> Fields { get; set; } = new();
}

public sealed class EquipmentExcelWorkbookDto
{
    public string FileName { get; set; } = string.Empty;
    public List<EquipmentExcelSheetDto> Sheets { get; set; } = new();
}

public sealed class EquipmentExcelSheetDto
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public bool HasData { get; set; }
}

/// <summary>
/// Một dòng thô của sheet Excel, dùng để người dùng xem trước và tự chọn
/// dòng tiêu đề / cột / vùng dữ liệu trước khi lấy schema.
/// </summary>
public sealed class EquipmentExcelGridRowDto
{
    /// <summary>Chỉ số dòng 0-based, tuyệt đối trong sheet (khớp với DataStartRowIndex/DataEndRowIndex khi submit).</summary>
    public int RowIndex { get; set; }
    /// <summary>true nếu dòng nằm trong một group Excel (Outline) hoặc đang bị ẩn (zero-height) — hệ thống sẽ luôn bỏ qua dòng này khi lấy schema/dữ liệu, không phụ thuộc vùng dữ liệu đã chọn.</summary>
    public bool IsGroupedOrHidden { get; set; }
    public List<string?> Cells { get; set; } = new();
}

public sealed class EquipmentExcelGridDto
{
    public string FileName { get; set; } = string.Empty;
    public int SheetIndex { get; set; }
    /// <summary>Tổng số dòng thực tế của sheet (kể cả dòng group/hidden), để UI biết còn dữ liệu phía dưới phần đã tải hay không.</summary>
    public int TotalRowCount { get; set; }
    public int ColumnCount { get; set; }
    /// <summary>Danh sách dòng đã tải (giới hạn theo MaxRows của request) để hiển thị preview.</summary>
    public List<EquipmentExcelGridRowDto> Rows { get; set; } = new();
}

/// <summary>
/// Vùng dữ liệu người dùng tự chọn trên preview: dòng tiêu đề, các cột cần lấy schema,
/// và vùng dữ liệu để hệ thống nhận định loại dữ liệu.
/// Toàn bộ chỉ số dòng/cột đều là 0-based, tuyệt đối trong sheet (giống RowIndex của EquipmentExcelGridRowDto).
/// </summary>
public sealed class EquipmentExcelRangeRequest
{
    public int SheetIndex { get; set; }
    /// <summary>Dòng chứa tiêu đề cột.</summary>
    public int HeaderRowIndex { get; set; }
    /// <summary>Các cột (0-based) sẽ lấy schema. Null hoặc rỗng = lấy tất cả cột có tiêu đề.</summary>
    public List<int>? SelectedColumnIndexes { get; set; }
    /// <summary>Dòng bắt đầu vùng dữ liệu. Null = HeaderRowIndex + 1.</summary>
    public int? DataStartRowIndex { get; set; }
    /// <summary>Dòng kết thúc vùng dữ liệu (bao gồm). Null = dòng cuối cùng của sheet.</summary>
    public int? DataEndRowIndex { get; set; }
}

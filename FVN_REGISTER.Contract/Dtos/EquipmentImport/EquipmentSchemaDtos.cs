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

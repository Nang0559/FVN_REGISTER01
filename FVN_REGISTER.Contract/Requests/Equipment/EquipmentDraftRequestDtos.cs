namespace FVN_REGISTER.Contract.Dtos.Equipment;

/// <summary>Editable fields of a registration/asset-change draft. Only valid while the request is Draft or NeedsRevision.</summary>
public sealed class UpdateEquipmentDraftRequest
{
    public string EquipmentName { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public string? SerialNumber { get; set; }
    public string? Specification { get; set; }
    public string? Location { get; set; }
    public string? Note { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? ExpectedDepreciationDate { get; set; }
    /// <summary>Custom (schema-driven) field values; null leaves the stored custom data unchanged.</summary>
    public Dictionary<string, string?>? CustomValues { get; set; }
}

/// <summary>Submit child requests of an import batch. Null or empty RequestIds means "all eligible children".</summary>
public sealed class EquipmentBatchSubmitRequest
{
    public List<int>? RequestIds { get; set; }
}

public sealed class EquipmentBatchSubmitResultDto
{
    public long BatchId { get; set; }
    public int Submitted { get; set; }
    public int Failed { get; set; }
    public List<int> SubmittedRequestIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

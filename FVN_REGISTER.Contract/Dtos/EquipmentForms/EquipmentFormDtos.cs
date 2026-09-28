namespace FVN_REGISTER.Contract.Dtos.EquipmentForms;

public sealed class EquipmentFormQuestionOptionDto
{
    public string OptionCode { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
}

public sealed class EquipmentFormQuestionDto
{
    public int Id { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "Text";
    public string? HelpText { get; set; }
    public string? Placeholder { get; set; }
    public bool IsRequired { get; set; }
    public int Sequence { get; set; }
    public List<EquipmentFormQuestionOptionDto> Options { get; set; } = new();
}

public sealed class EquipmentApplicableFormDto
{
    public int FormId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CategoryCode { get; set; }
    public int Version { get; set; }
    public bool RequireApproval { get; set; }
    public int DisplayOrder { get; set; }
    public int AssetId { get; set; }
    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public List<EquipmentFormQuestionDto> Questions { get; set; } = new();
}

public sealed class EquipmentFormAssignmentDto
{
    public int Id { get; set; }
    public string EquipmentSchemaKey { get; set; } = string.Empty;
    public int FormId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string FormTitle { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActiveAssignment { get; set; }
}

public sealed class EquipmentFormAssignmentRequest
{
    public string EquipmentSchemaKey { get; set; } = string.Empty;
    public int FormId { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class EquipmentFormAnswerRequest
{
    public int QuestionId { get; set; }
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BoolValue { get; set; }
    public string? JsonValue { get; set; }
}

public sealed class EquipmentFormSubmissionRequest
{
    public int AssetId { get; set; }
    public int FormId { get; set; }
    public List<EquipmentFormAnswerRequest> Answers { get; set; } = new();
}

public sealed class EquipmentFormSubmissionDto
{
    public int SubmissionId { get; set; }
    public int EquipmentRequestId { get; set; }
    public int AssetId { get; set; }
    public int FormId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequireApproval { get; set; }
}

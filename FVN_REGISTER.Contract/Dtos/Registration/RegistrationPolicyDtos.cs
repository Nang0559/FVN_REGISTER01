using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.Registration;

public sealed class RegistrationPolicyDto
{
    public int Id { get; set; }
    public int DepartmentCode { get; set; }
    public string RegisterType { get; set; } = "OT";
    public RegistrationRuleMode Mode { get; set; }
    public bool IsActive { get; set; }
    public List<int> UserIds { get; set; } = new();
    public List<string> PositionCodes { get; set; } = new();
    public List<string> SelfRegistrationPositionCodes { get; set; } = new();
}

public sealed class RegistrationPolicyUpsertDto
{
    public int DepartmentCode { get; set; }
    public string RegisterType { get; set; } = "OT";
    public RegistrationRuleMode Mode { get; set; } = RegistrationRuleMode.AssignedPositions;
    public bool IsActive { get; set; } = true;
    public List<int> UserIds { get; set; } = new();
    public List<string> PositionCodes { get; set; } = new();
    public List<string> SelfRegistrationPositionCodes { get; set; } = new();
}

public sealed class RegistrationPolicyDecisionDto
{
    public int DepartmentCode { get; set; }
    public string RegisterType { get; set; } = "OT";
    public RegistrationRuleMode Mode { get; set; }
    /// <summary>true = được đăng ký OT/Trip (cho mình và nhiều người trong phạm vi). false = phải báo cấp quản lý / người được chỉ định.</summary>
    public bool CanRegister { get; set; }
    public bool CanRegisterForOthers { get; set; }
    public bool CanRegisterSelfOnly { get; set; }
    public bool IsFallback { get; set; }
}

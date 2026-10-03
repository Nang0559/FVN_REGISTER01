namespace FVN_REGISTER.Core.Enums;

public enum EndpointGovernanceItemType
{
    Software = 1,
    WindowsService = 2,
    Antivirus = 3
}

public enum EndpointTargetType
{
    Workstation = 1,
    Server = 2,
    Both = 3
}

public enum EndpointGovernanceRequestType
{
    InstallSoftware = 1,
    NewSoftware = 2,
    ServiceChange = 3,
    Exception = 4,
    SoftwarePolicyChange = 5,
    ServicePolicyChange = 6
}

public enum EndpointComplianceResult
{
    Compliant = 1,
    NonCompliant = 2,
    Exception = 3,
    Unknown = 4
}

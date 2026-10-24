namespace FVN_REGISTER.Core.Enums;

/// <summary>How a department decides who may register OT/Trip for other employees.</summary>
public enum RegistrationRuleMode
{
    Everyone = 1,
    AssignedUsers = 2,
    AssignedPositions = 3
}

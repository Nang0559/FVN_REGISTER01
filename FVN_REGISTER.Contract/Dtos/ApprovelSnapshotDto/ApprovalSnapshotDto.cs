
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto
{
    public sealed record ApprovalSnapshotDto(
        int RequestId,
        RequestModule ModuleName,
        DateTime CapturedAt,
        IReadOnlyList<ApprovalStepSnapshotDto> Steps,
        string? RequesterEmployeeCode = null
    );
}

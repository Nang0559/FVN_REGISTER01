using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Common
{
    [Table("F03ApprovalSnapshots")]
    public sealed class F03ApprovalSnapshot : BaseAuditEntity
    {
        public int RequestId { get; init; }
        public RequestModule RequestType { get; init; }

        /// <summary>
        /// Employee identity used to resolve the approval route. It is frozen with
        /// the snapshot so later HRM changes cannot silently change the approver context.
        /// </summary>
        public string? RequesterEmployeeCode { get; init; }

        public ICollection<F03ApprovalStepSnapshot> Steps { get; init; } = new List<F03ApprovalStepSnapshot>();
    }
}

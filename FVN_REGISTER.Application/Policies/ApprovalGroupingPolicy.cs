using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Policies
{
    public class ApprovalGroupingPolicy : IApprovalGroupingPolicy
    {
        public List<PendingApprovalGroupDto> BuildGroups(
            IReadOnlyDictionary<RequestModule, List<PendingApprovalItemDto>> byModule)
        {
            if (byModule == null || byModule.Count == 0)
                return new List<PendingApprovalGroupDto>();

            var flat = byModule
                .SelectMany(kv => kv.Value)
                .Where(item => item.CanApprove)
                .ToList();

            var groups = flat
                .GroupBy(item => new
                {
                    item.Kind,
                    ApproverLevel = GetCurrentApprovalLevel(item)
                })
                .Where(g => g.Key.ApproverLevel > 0)
                .Select(g => new PendingApprovalGroupDto
                {
                    RequestType = g.Key.Kind,
                    ApproverLevel = g.Key.ApproverLevel,
                    Count = g.Count(),
                    OverriddenCount = g.Count(i => i.ApprovalSteps.Any(s => s.IsOverriddenByAdmin)),
                    Requests = SortItems(g.ToList())
                })
                .ToList();

            return SortGroups(groups);
        }

        private static int GetCurrentApprovalLevel(PendingApprovalItemDto item)
            => item.ApprovalSteps
                .OrderBy(s => s.Level)
                .FirstOrDefault(s => s.IsRequired && s.IsApproved == null)?.Level ?? 0;

        private static List<PendingApprovalGroupDto> SortGroups(List<PendingApprovalGroupDto> groups)
            => groups
                .OrderBy(g => ModulePriority(g.RequestType))
                .ThenBy(g => g.ApproverLevel)
                .ToList();

        private static int ModulePriority(RequestModule module) => module switch
        {
            RequestModule.Leave => 0,
            RequestModule.Overtime => 1,
            RequestModule.Trip => 2,
            RequestModule.Equipment => 3,
            RequestModule.Attendance => 4,
            RequestModule.Payroll => 5,
            _ => 99
        };

        private static List<PendingApprovalItemDto> SortItems(List<PendingApprovalItemDto> items)
            => items
                .OrderBy(i => i.TimeoutDays ?? int.MaxValue)
                .ThenBy(i => i.SubmittedAt ?? DateTime.MaxValue)
                .ThenBy(i => i.RequestId)
                .ToList();
    }
}

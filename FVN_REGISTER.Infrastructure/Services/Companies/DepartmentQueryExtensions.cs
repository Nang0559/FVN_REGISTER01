using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Utils;

namespace FVN_REGISTER.Infrastructure.Services.Companies
{
    public static class DepartmentQueryExtensions
    {
        public static IQueryable<F03Department> WhereActiveDept(
            this IQueryable<F03Department> query)
            => query.Where(x => x.IsActive == true);

        public static IQueryable<F03Department> WhereDeptCode(
            this IQueryable<F03Department> query, string? deptCode)
        {
            if (string.IsNullOrWhiteSpace(deptCode))
                return query;

            var code = DepartmentCodeParser.ParseRequired(deptCode, nameof(deptCode));
            return query.Where(x => x.DeptCode == code);
        }
    }
}

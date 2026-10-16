using FVN_REGISTER.Core.Entities.HR;


namespace FVN_REGISTER.Infrastructure.Services.Companies
{
    public static class DepartmentQueryExtensions
    {
        public static IQueryable<F03Department> WhereActiveDept(
            this IQueryable<F03Department> query)
            => query.Where(x => x.IsActive==true);

        public static IQueryable<F03Department> WhereDeptCode(
            this IQueryable<F03Department> query, int? deptCode)
            => deptCode == null ? query : query.Where(x => x.DeptCode == deptCode);
    }
}

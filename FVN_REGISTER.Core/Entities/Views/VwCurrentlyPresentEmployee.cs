using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Core.Entities.Views;

public partial class VwCurrentlyPresentEmployee
{
    public string EmployeeId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int? DeptCode { get; set; }

    public string? DeptName { get; set; }

    public DateOnly? Date { get; set; }

    public DateTime? CheckInTime { get; set; }
}

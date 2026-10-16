namespace FVN_REGISTER.Contract.Dtos.PublicForms;

/// <summary>
/// What the current user may do in the "Công khai" module. Computed server-side so the UI never
/// has to guess from RBAC codes alone (operators assigned in Admin/Security are not visible in the
/// permission snapshot).
/// </summary>
public sealed class PublicCapabilitiesDto
{
    /// <summary>Can open /public-forms and register (PublicForm.View, Own).</summary>
    public bool CanRegister { get; set; }

    /// <summary>Can create/edit/publish public information (PublicInformation.Manage + operator assignment).</summary>
    public bool CanManageInformation { get; set; }

    /// <summary>Can create/edit/publish/close registration forms (PublicForm.Manage operator).</summary>
    public bool CanManageForms { get; set; }

    /// <summary>Can view the registration results (PublicForm.SubmissionView operator).</summary>
    public bool CanViewResults { get; set; }
}

namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IAuthorizedAIPlatformAdministration
{
    Task AuthorizeAsync(string tenantCode, string actorId, string action, CancellationToken cancellationToken = default);
}

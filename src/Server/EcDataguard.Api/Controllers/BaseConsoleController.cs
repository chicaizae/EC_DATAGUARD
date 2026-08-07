using Microsoft.AspNetCore.Mvc;

namespace EcDataguard.Api.Controllers;

[ApiController]
public abstract class BaseConsoleController : ControllerBase
{
    protected Guid? EffectiveTenantScopeOrNull()
    {
        if (User.IsSuperAdmin())
        {
            return null;
        }

        return User.GetScopeTenantId() ?? User.GetTenantId();
    }

    protected string CurrentActorName()
        => User.FindFirst("email")?.Value ?? User.FindFirst("sub")?.Value ?? "system";

    protected Guid? CurrentActorId()
        => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : null;
}

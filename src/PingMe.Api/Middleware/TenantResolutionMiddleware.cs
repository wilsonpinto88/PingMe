namespace PingMe.Api.Middleware;

using PingMe.Infrastructure.Tenants;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CurrentTenantProvider currentTenantProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenantId")?.Value;
            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var tenantId))
            {
                currentTenantProvider.TenantId = tenantId;
            }
        }

        await _next(context);
    }
}

namespace PingMe.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using PingMe.Domain.Common;

public class AppUser : IdentityUser<Guid>, ITenantOwned
{
    public AppUser()
    {
        Id = Guid.NewGuid();
    }

    public Guid TenantId { get; set; }
}

namespace PingMe.Domain.Tenants;

using PingMe.Domain.Common;

public class Tenant : Entity
{
    public string Name { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    private Tenant() { }

    public Tenant(string name, DateTime createdAt)
    {
        Name = name;
        CreatedAt = createdAt;
    }
}

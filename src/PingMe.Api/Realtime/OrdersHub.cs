namespace PingMe.Api.Realtime;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize(Roles = "Owner,Staff")]
public class OrdersHub : Hub
{
    private readonly ILogger<OrdersHub> _logger;

    public OrdersHub(ILogger<OrdersHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
        }
        else
        {
            _logger.LogWarning(
                "Authenticated connection {ConnectionId} had no tenantId claim — it will never receive order broadcasts.",
                Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }
}

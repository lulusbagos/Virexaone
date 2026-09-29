using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Virexaone.FMS.Backend.Hubs
{
    public class TelemetryHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
    }
}

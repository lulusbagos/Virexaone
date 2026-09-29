using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Virexaone.FMS.Backend.Hubs;

namespace Virexaone.FMS.Backend.Services
{
    public class TelemetryStreamService : BackgroundService
    {
        private readonly IHubContext<TelemetryHub> _hubContext;
        private readonly FmsDataService _fmsDataService;
        private readonly ILogger<TelemetryStreamService> _logger;

        public TelemetryStreamService(
            IHubContext<TelemetryHub> hubContext,
            FmsDataService fmsDataService,
            ILogger<TelemetryStreamService> logger)
        {
            _hubContext = hubContext;
            _fmsDataService = fmsDataService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Real-Time Telemetry Streaming Service started (5.0s interval).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var fleet = await _fmsDataService.GetLiveFleetAsync();
                    int feedAgeSeconds = _fmsDataService.FleetFeedAgeSeconds;
                    var packet = new
                    {
                        @event = "fleet_update",
                        timestamp = DateTime.UtcNow.ToString("o"),
                        feed_age_seconds = feedAgeSeconds,
                        feed_stale = feedAgeSeconds > 120,
                        count = fleet.Count,
                        units = fleet
                    };

                    await _hubContext.Clients.All.SendAsync("ReceiveFleetTelemetry", packet, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Telemetry stream exception: {ex.Message}");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

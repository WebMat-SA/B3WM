using B3WM.Client.Services;
using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MudBlazor.Charts;
using System.Globalization;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DashboardController : ControllerBase
    {
        private readonly IHubContext<DataHub, IDataHubClient> hub;
        private readonly IEnumerable<CandleService> candleService;
        private readonly ILogger<DashboardController> logger;
        private readonly IEnumerable<BubbleService> bubbleService;
        private readonly IEnumerable<VolumeService> volumeService;

        public DashboardController(IHubContext<DataHub,IDataHubClient> hub, IEnumerable<CandleService> _candleService, ILogger<DashboardController> logger, IEnumerable<BubbleService> bubbleService, IEnumerable<VolumeService> volumeService)
        {
            this.hub = hub;
            this.logger = logger;
            this.candleService = _candleService;
            this.bubbleService = bubbleService;
            this.volumeService = volumeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            List<object> data = new();
            foreach(var iprocess in candleService)
            {
                data.Add(iprocess.GetSnapshot());
            }

            foreach (var iprocess in bubbleService)
            {
                data.Add(iprocess.GetSnapshot());
            }

            foreach (var iprocess in volumeService)
            {
                data.Add(iprocess.GetSnapshot());
            }

            // ✔️ retorna imediatamente
            return Ok(System.Text.Json.JsonSerializer.Serialize(data));
        }
    }
}

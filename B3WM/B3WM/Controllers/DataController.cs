using B3WM.Client.Services;
using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.Mvc;
namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DataController : ControllerBase
    {
        private readonly DataKeeperBase dataKeeper;
        private readonly IEnumerable<StructureService> structureServices;

        public DataController(DataKeeperBase dataKeeper, IEnumerable<StructureService> structureServices)
        {
            this.dataKeeper = dataKeeper;
            this.structureServices = structureServices;
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> GetBarAsync(string symbol, DateTime date)
        {

            List<BarStorageItem> data = new List<BarStorageItem>();

            foreach(var timeFrame in Defaults.TimeFrames)
            {
                string path = $"{symbol}_{nameof(CandleService)}_{timeFrame}MIN_{date:yyyy-MM-dd}.json";

                data.AddRange(await dataKeeper.ReadDataAsync<List<BarStorageItem>>(path));
            }

            // ✔️ retorna imediatamente
            return Ok(System.Text.Json.JsonSerializer.Serialize(data));
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> GetBubbleAsync(string symbol, DateTime date)
        {
            string path = $"{symbol}_{nameof(BubbleService)}_{date:yyyy-MM-dd}.json";

            var data = await dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(path);

            // ✔️ retorna imediatamente
            return Ok(System.Text.Json.JsonSerializer.Serialize(data));
        }

        [HttpGet("{symbol}/{date}/{minDistance:double}")]
        public async Task<IActionResult> GetStructureAsync(string symbol, DateTime date, double minDistance)
        {

            List<StructureStorageItem> data = new List<StructureStorageItem>();

            foreach (var timeFrame in Defaults.TimeFrames)
            {
                string path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{minDistance}_{date:yyyy-MM-dd}.json";

                data.AddRange(await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path));
            }

            // ✔️ retorna imediatamente
            return Ok(System.Text.Json.JsonSerializer.Serialize(data));
        }

        [HttpGet("{symbol}/{minDistance:double}")]
        public async Task<IActionResult> SetStructureDistanceAsync(string symbol,double minDistance)
        {
            foreach(var structure in structureServices)
            {
                await structure.SetMinDistance(minDistance);
            }

            return await GetStructureAsync(symbol, DateTime.Today, minDistance);
        }
    }
}

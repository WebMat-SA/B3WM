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

        public DataController(DataKeeperBase dataKeeper)
        {
            this.dataKeeper = dataKeeper;
        }

        [HttpGet("{symbol}/{timeFrame}/{date}")]
        public async Task<IActionResult> GetBarAsync(string symbol, int timeFrame, DateTime date)
        {
            string path = $"{symbol}_{nameof(CandleService)}_{timeFrame}MIN_{date:yyyy-MM-dd}.json";

            var data = await dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);

            // ✔️ retorna imediatamente
            return Ok(System.Text.Json.JsonSerializer.Serialize(data));
        }
    }
}

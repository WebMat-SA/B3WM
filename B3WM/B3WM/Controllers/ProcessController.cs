using B3WM.Client.Services;
using B3WM.Services;
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
    public class ProcessController : ControllerBase
    {
        private readonly IHubContext<DataHub, IDataHubClient> hub;

        public ProcessController(IHubContext<DataHub,IDataHubClient> hub)
        {
            this.hub = hub;
        }

        [HttpPost("{Symbol}/{Date}/{startTime}/{endTime}")]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Process(IFormFile file,[FromHeader(Name = "X-ConnectionId")] string connectionId,string Symbol, DateTime Date, TimeSpan startTime, TimeSpan endTime)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Arquivo inválido");

            var tempPath = Path.GetTempFileName();

            using (var fs = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(fs);

            // 🔥 dispara processamento em background
            _ = Task.Run(() => ProcessFile(tempPath, connectionId, Symbol, Date,startTime, endTime));

            // ✔️ retorna imediatamente
            return Ok(new
            {
                Message = "Processamento iniciado"
            });
        }

        private async Task ProcessFile(string path, string connectionId,string Symbol, DateTime Date, TimeSpan startTime, TimeSpan endTime)
        {
            try
            {
                const int batchSize = 1000;

                var batch = new List<Ticks2>(batchSize);

                foreach (var linha in FileHelper.ReadLinesReverse(path))
                {
                    if (!linha.StartsWith("\""))
                        continue;

                    Console.WriteLine(linha);

                    //converte para tick
                    var tick2 = ConvertToTick2(linha, Date, Symbol);

                    //verifica se ticks estão dentro do time
                    if (tick2 == null || tick2.Time.TimeOfDay <= startTime || tick2.Time.TimeOfDay >= endTime)
                        continue;

                    //converte para tick2
                    batch.Add(tick2);

                    if (batch.Count >= batchSize)
                    {

                        var jsonData = System.Text.Json.JsonSerializer.Serialize(batch);

                        await hub.Clients.Client(connectionId).ReceiveCsvLines(jsonData);

                        

                        batch.Clear();
                    }
                }

                // envia resto
                if (batch.Count > 0)
                {

                    var jsonData = System.Text.Json.JsonSerializer.Serialize(batch);

                    await hub.Clients.Client(connectionId).ReceiveCsvLines(jsonData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                System.IO.File.Delete(path);
            }


        }

        private Ticks2 ConvertToTick2(string line, DateTime Date, string Symbol)
        {
            var parts = DataHelper.ParseCsvLine(line);

            if (parts.Length < 7)
                return null;

            if (!TimeSpan.TryParse(parts[0], out var time))
                return null;


            return new Ticks2
            {
                Time = Date.Date + TimeSpan.Parse(parts[0]),

                Volume = int.Parse(parts[1].Replace(".", "")),

                Value = double.Parse(
                    parts[2],
                    CultureInfo.GetCultureInfo("pt-BR")
                ),

                TrydID = int.Parse(parts[3]),

                Buyer = DataHelper.ParseAgent(parts[4]),
                Seller = DataHelper.ParseAgent(parts[5]),

                Starter = DataHelper.ParseActionType(parts[6]),

                Symbol = Symbol ?? "Desconhecido"
            };

            
        }
    }
}

using B3WM.Services;
using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Process(IFormFile file,[FromHeader(Name = "X-ConnectionId")] string connectionId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Arquivo inválido");

            var tempPath = Path.GetTempFileName();

            using (var fs = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(fs);

            // 🔥 dispara processamento em background
            _ = Task.Run(() => ProcessFile(tempPath, connectionId));

            // ✔️ retorna imediatamente
            return Ok(new
            {
                Message = "Processamento iniciado"
            });
        }

        private async Task ProcessFile(string path, string connectionId)
        {
            try
            {
                var batch = new List<string>(150);

                foreach (var linha in FileHelper.ReadLinesReverse(path))
                {
                    if (!linha.StartsWith("\""))
                        continue;

                    batch.Add(linha);

                    if (batch.Count >= 150)
                    {
                        await hub.Clients.Client(connectionId).ReceiveCsvLines(batch.ToArray());

                        Console.WriteLine(linha);

                        batch.Clear();
                    }
                }

                // envia resto
                if (batch.Count > 0)
                {
                    await hub.Clients.Client(connectionId).ReceiveCsvLines(batch.ToArray());
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
    }
}

using B3WM.Client.Pages;
using B3WM.Client.Services;
using B3WM.Shared.Entity;
using BlazorWorker.BackgroundServiceFactory;
using BlazorWorker.Core;
using BlazorWorker.WorkerBackgroundService;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace B3WM.Client.Model
{
    public class ImportNode
    {
        public Func<BarStorageItem, Task> NewBar { get; private set; }
        public Func<BubbleStorageItem, Task> NewBubble { get; private set; }

        public IBrowserFile File { get; set; } = default!;
        public DateTime? Date { get; set; } = DateTime.Today;
        public string? Symbol { get; set; }

        public bool Processing { get; set; } = false;
        public bool Processed { get; set; } = false;

        public bool CanDelete => !Processed && !Processing;
        public long MbSize => File == null ? 0 : File.Size / (long)(1024 * 1024);

        public static DateTime TryExtractDateFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return DateTime.Today;

            // Procura padrões yyyy-MM-dd OU dd-MM-yyyy
            var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}|\d{2}-\d{2}-\d{4}");

            if (!match.Success)
                return DateTime.Today;

            var dateStr = match.Value;

            // Tenta converter nos dois formatos possíveis
            if (DateTime.TryParseExact(
                    dateStr,
                    new[] { "yyyy-MM-dd", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return date;
            }

            return DateTime.Today;
        }
        public static string TryExtractSymbolFromFileName(string fileName, string failMatch)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return failMatch;

            // Padrão de contratos futuros da B3
            var match = Regex.Match(
                fileName.ToUpper(),
                @"[A-Z]{3}[FGHJKMNQUVXZ]\d{2}"
            );

            return match.Success ? match.Value : failMatch;
        }

        private IWorker? MainWorker;
        private IWorkerBackgroundService<MainHelper>? MainService { get; set; }

        //candles, controle para enviar pra tela principal
        public ConcurrentQueue<BarStorageItem> Bars { get; set; } = new();
        //bubbles, controle para enviar para tela principal
        public ConcurrentQueue<BubbleStorageItem> Bubbles { get; set; } = new();

        //ultimo volume feito
        private VolumeLevelStorageItem? _lastVolumeLevel { get; set; }

        public IWorkerFactory workerFactory { get; private set; }

        public decimal Progress { get; private set; }
        public DateTime? Start { get; private set; }

        public ImportNode(IBrowserFile _file, DateTime _date, string _symbol, IWorkerFactory _workerFactory, Func<BarStorageItem, Task> newBar, Func<BubbleStorageItem, Task> newBubble)
        {
            File = _file;
            Date = _date;
            Symbol = _symbol;
            workerFactory = _workerFactory;

            NewBar = newBar;
            NewBubble = newBubble;
        }

        public async Task CreateWorkers(int _timeFrame, int _bubbleThreshold)
        {
            try
            {

                HelperPerformanceConfig.Log(nameof(Import), "CreateWorkers", 0, $"{MainService != null}");

                if (MainWorker == null)
                    MainWorker = await workerFactory.CreateAsync();

                if (MainService != null)
                    return;

                MainService = await MainWorker.CreateBackgroundServiceAsync<MainHelper>();

                RegisterCandleEvents();
                RegisterBubbleEvents();
                RegisterVolumeEvents();

                await MainService.RunAsync(s => s.InitCandle(int.MaxValue, _timeFrame, true, true));
                await MainService.RunAsync(s => s.InitBubble(int.MaxValue, _bubbleThreshold, true, true));
                await MainService.RunAsync(s => s.InitVolume(2000, true));

                HelperPerformanceConfig.Log(nameof(Import), "CreateWorkers", 0, $"{MainService != null}");

            }
            catch (Exception e)
            {
                HelperPerformanceConfig.Log(nameof(Import), "CreateWorkers", 0, $"Error: {e.Message}");
            }
        }

        #region Candle
        private void RegisterCandleEvents()
        {
            MainService!.RegisterEventListenerAsync(nameof(MainHelper.Candle_OnClosedBars),
                async (object? s, BarStorageItem data) =>
                {
                    var sw = Stopwatch.StartNew();

                    //vincula o volume no candle
                    data.VolumeLevel = _lastVolumeLevel?.Volumes;

                    //notiifca o pai
                    if (NewBar != null) await NewBar.Invoke(data);

                    //enqueeu
                    Bars.Enqueue(data);
                    


                    sw.Stop();
                    // HelperPerformanceConfig.Log(nameof(Import), "Candle_OnClosedBars",
                    //     sw.ElapsedMilliseconds,
                    //     $"Received new closed bars at {DateTime.Now:HH:mm:ss.fff}");
                });
        }
        #endregion

        #region Bubble
        private void RegisterBubbleEvents()
        {
            MainService!.RegisterEventListenerAsync(nameof(MainHelper.Bubble_OnNewBubble),
                async (object? s, BubbleStorageItem data) =>
                {
                    var sw = Stopwatch.StartNew();

                    if (NewBubble != null) await NewBubble.Invoke(data);

                    Bubbles.Enqueue(data);

                    sw.Stop();
                    // HelperPerformanceConfig.Log(nameof(HubClient), "Bubble_OnNewBubble",
                    //     sw.ElapsedMilliseconds,
                    //     $"Received new bubble at {DateTime.Now:HH:mm:ss.fff}");
                });
        }
        #endregion

        #region Volume
        private void RegisterVolumeEvents()
        {

            MainService.RegisterEventListenerAsync(nameof(MainHelper.Volume_OnVolumeUpdate),
                (object? s, VolumeLevelStorageItem data) =>
                {
                    var sw = Stopwatch.StartNew();

                    //atualiza virtualmente
                    _lastVolumeLevel = data;

                    sw.Stop();
                    // HelperPerformanceConfig.Log(nameof(HubClient), "Volume_OnUpdateLastVolume",
                    //     sw.ElapsedMilliseconds,
                    //     $"Received new current volume at {DateTime.Now:HH:mm:ss.fff}");
                });
        }

        #endregion

        public async Task Process(int _timeFrame, int _bubbleThreshold, TimeSpan? startAt)
        {
            Start = DateTime.Now;

            await CreateWorkers(_timeFrame, _bubbleThreshold);

            using (var stream = File.OpenReadStream(1024 * 1024 * 1024))
            {
                if (MainService != null)
                {
                    var swTotal = Stopwatch.StartNew();

                    int count = 0;
                    int batch = 1000;
                    List<Ticks2> batchList = new();

                    //fazer batch do envio aqui
                    await foreach (var tick in DataHelper.ParseTicks2FromCsv(stream, Date ?? DateTime.Today, Symbol ?? "UNKNOWN", startAt))
                    {
                        batchList.Add(tick);
                        count++;

                        if (batchList.Count >= batch)
                        {
                            var jsonData = System.Text.Json.JsonSerializer.Serialize(batchList);

                            //Console.WriteLine(jsonData);

                            await MainService.RunAsync(s => s.EnqueueFromCsv(jsonData));

                            batchList.Clear();
                            Progress = (decimal)count ;
                        }
                    }

                    //ultimo envio
                    var jsonDataLast = System.Text.Json.JsonSerializer.Serialize(batchList.ToArray());

                    Console.WriteLine(jsonDataLast);

                    await MainService.RunAsync(s => s.EnqueueFromCsv(jsonDataLast));

                    swTotal.Stop();
                    //HelperPerformanceConfig.Log(nameof(HubClient), "RunLoop.SendingToWebWorker()", swTotal.ElapsedMilliseconds, $"tick at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
        }

        public void PrintDebug()
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(Bars));
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(Bubbles));
        }
    }
}

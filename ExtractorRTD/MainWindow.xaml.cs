using ExtractorRTD.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ExtractorRTD
{
    public partial class MainWindow : Window
    {
        private const string OtherKey = "OUTROS";

        public string[] DataTypes { get; set; } =
        {
            "WINFUT",
            "INDFUT",
            "WDOFUT",
            "DOLFUT"
        };

        public string Url { get; set; } =
            "https://localhost:5002/api/datahub";

        // Uma fila/coleção de log por símbolo (issue #47): o mapeamento T&T
        // é editável e nada mais é fixo em WIN/WDO.
        private readonly Dictionary<string, ConcurrentQueue<string>> _buffers =
            new(StringComparer.OrdinalIgnoreCase);

        // CONFIGURACAO DOS T&Ts
        public ObservableCollection<TntItem> Tnts { get; set; } =
            new ObservableCollection<TntItem>
            {
                new TntItem
                {
                    TNTSymbol = "T&T0",
                    Symbol = "WINFUT"
                },

                // T0/T1 mantêm o mapeamento legado; os cheios entram em T2/T3
                // para não quebrar quem já usa só WIN/WDO.
                new TntItem
                {
                    TNTSymbol = "T&T1",
                    Symbol = "WDOFUT"
                },

                new TntItem
                {
                    TNTSymbol = "T&T2",
                    Symbol = "INDFUT"
                },

                new TntItem
                {
                    TNTSymbol = "T&T3",
                    Symbol = "DOLFUT"
                }
            };

        // LOGS SEPARADOS (um tab por símbolo + OUTROS)
        public ObservableCollection<SymbolLog> SymbolLogs { get; set; } =
            new ObservableCollection<SymbolLog>();

        BackgroundWorker workerTnT =
            new BackgroundWorker();

        CancellationTokenSource SourceTnT { get; set; }

        CancellationToken TokenTnT { get; set; }

        BackgroundWorker workerBook = new BackgroundWorker();

        CancellationTokenSource SourceBook { get; set; }

        CancellationToken TokenBook { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            foreach (var symbol in DataTypes)
            {
                EnsureSymbolLog(symbol);
            }

            EnsureSymbolLog(OtherKey);

            DataContext = this;

            DispatcherTimer timer =
            new DispatcherTimer();

            timer.Interval =
                TimeSpan.FromMilliseconds(100);

            timer.Tick += FlushUi;

            timer.Start();
        }

        private void EnsureSymbolLog(string symbol)
        {
            if (_buffers.ContainsKey(symbol))
                return;

            _buffers[symbol] = new ConcurrentQueue<string>();

            SymbolLogs.Add(
                new SymbolLog(
                    symbol,
                    new ObservableCollection<string>()));
        }

        private ConcurrentQueue<string> QueueFor(string symbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol) &&
                _buffers.TryGetValue(symbol, out var queue))
            {
                return queue;
            }

            return _buffers[OtherKey];
        }

        private void LogOther(string message)
        {
            QueueFor(OtherKey).Enqueue(message);
        }

        private void FlushUi(
            object sender,
            EventArgs e)
        {
            foreach (var log in SymbolLogs)
            {
                if (_buffers.TryGetValue(log.Symbol, out var queue))
                {
                    FlushQueue(queue, log.Messages);
                }
            }

            SwapButton.IsEnabled = !IsRtdRunning();
        }

        private void FlushQueue(
            ConcurrentQueue<string> queue,
            ObservableCollection<string> target)
        {
            int count = 0;

            while (queue.TryDequeue(out string msg))
            {
                target.Insert(0, msg);

                count++;

                if (count >= 50)
                    break;
            }

            while (target.Count > 200)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

        private void TimesAndTradesProfit_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!workerTnT.IsBusy ||
                SourceTnT == null ||
                SourceTnT.IsCancellationRequested)
            {
                workerTnT =
                    new BackgroundWorker();

                workerTnT.WorkerReportsProgress = true;

                workerTnT.WorkerSupportsCancellation = true;

                workerTnT.DoWork += WorkProfit;

                workerTnT.ProgressChanged += Print;

                SourceTnT =
                    new CancellationTokenSource();

                TokenTnT =
                    SourceTnT.Token;

                workerTnT.RunWorkerAsync();
            }
            else
            {
                SourceTnT.Cancel();

                workerTnT.CancelAsync();

                TimesAndTradesRtd.Stop();
            }
        }

        private void WorkProfit(
            object obj,
            DoWorkEventArgs e)
        {
            try
            {
                TimesAndTradesRtd.Start(
                    Tnts
                    .Select(x => new TntConfig
                    {
                        TNTSymbol = x.TNTSymbol,
                        Symbol = x.Symbol
                    })
                    .ToList(),
                    Url,
                    workerTnT,
                    TokenTnT);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogOther(ex.ToString());
                });
            }
        }

        private void BookLvl2_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!workerBook.IsBusy ||
                SourceBook == null ||
                SourceBook.IsCancellationRequested)
            {
                workerBook = new BackgroundWorker();

                workerBook.WorkerReportsProgress = true;

                workerBook.WorkerSupportsCancellation = true;

                workerBook.DoWork += WorkBook;

                workerBook.ProgressChanged += PrintBook;

                SourceBook = new CancellationTokenSource();

                TokenBook = SourceBook.Token;

                workerBook.RunWorkerAsync();
            }
            else
            {
                SourceBook.Cancel();

                workerBook.CancelAsync();

                BookLvl2.Stop();
            }
        }

        private void WorkBook(
            object obj,
            DoWorkEventArgs e)
        {
            try
            {
                BookLvl2.Start(
                    Tnts
                    .Select(x => x.Symbol)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList(),
                    Url,
                    workerBook,
                    TokenBook);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogOther(ex.ToString());
                });
            }
        }

        private void PrintBook(
            object s,
            ProgressChangedEventArgs ev)
        {
            if (ev.UserState is TickLogItem item)
            {
                QueueFor(item.Symbol).Enqueue(item.Message);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusTextBlockBook.Text =
                    BookLvl2.ConnectionState + " (LVL2)";
            }));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                SourceBook?.Cancel();
                BookLvl2.Stop();
            }
            catch
            {
            }

            try
            {
                SourceTnT?.Cancel();
                TimesAndTradesRtd.Stop();
            }
            catch
            {
            }

            base.OnClosing(e);
        }

        private void Print(
            object s,
            ProgressChangedEventArgs ev)
        {
            if (ev.UserState is TickLogItem item)
            {
                QueueFor(item.Symbol).Enqueue(item.Message);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusTextBlock.Text =
                    TimesAndTradesRtd.ConnectionState;

                StatusTextBlockData.Text =
                    $"{ev.ProgressPercentage} Ticks/s";
            }));
        }

        private void Swap_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (IsRtdRunning())
            {
                MessageBox.Show(
                    "Pare o RTD antes de fazer o swap das linhas T&T.",
                    "Extractor RTD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // Generalizado (issue #47): troca as duas primeiras linhas T&T
            // mapeadas, qualquer que seja o número (T0/T1 ou T2/T3).
            var rows = Tnts
                .Where(x => !string.IsNullOrWhiteSpace(x.TNTSymbol))
                .Take(2)
                .ToList();

            if (rows.Count < 2)
            {
                MessageBox.Show(
                    "Preciso de pelo menos duas linhas T&T mapeadas.",
                    "Extractor RTD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string tmp = rows[0].Symbol;
            rows[0].Symbol = rows[1].Symbol;
            rows[1].Symbol = tmp;

            LogOther(
                $"Swap: {rows[0].TNTSymbol}={rows[0].Symbol}, " +
                $"{rows[1].TNTSymbol}={rows[1].Symbol}");
        }

        private bool IsRtdRunning()
        {
            return workerTnT != null &&
                workerTnT.IsBusy &&
                SourceTnT != null &&
                !SourceTnT.IsCancellationRequested;
        }

        private async void Clear_Click(
            object sender,
            RoutedEventArgs e)
        {
            foreach (var log in SymbolLogs)
            {
                log.Messages.Clear();
            }

            TimesAndTradesRtd.Counter = 0;

            if (TimesAndTradesRtd.hubConnection == null ||
                TimesAndTradesRtd.hubConnection.State !=
                HubConnectionState.Connected)
            {
                TimesAndTradesRtd.StartHubConnection();
            }

            await TimesAndTradesRtd.hubConnection.SendAsync(
                "SendDataTnT",
                Encoding.ASCII.GetBytes("CLEAR"));
        }
    }

    public class SymbolLog
    {
        public string Symbol { get; }

        public ObservableCollection<string> Messages { get; }

        public SymbolLog(string symbol, ObservableCollection<string> messages)
        {
            Symbol = symbol;
            Messages = messages;
        }
    }

    public class TntItem : INotifyPropertyChanged
    {
        private string _tntSymbol;
        private string _symbol;

        public string TNTSymbol
        {
            get => _tntSymbol;
            set
            {
                if (_tntSymbol != value)
                {
                    _tntSymbol = value;
                    OnPropertyChanged(nameof(TNTSymbol));
                }
            }
        }

        public string Symbol
        {
            get => _symbol;
            set
            {
                if (_symbol != value)
                {
                    _symbol = value;
                    OnPropertyChanged(nameof(Symbol));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
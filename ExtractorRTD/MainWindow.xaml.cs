using ExtractorRTD.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
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
        public string[] DataTypes { get; set; } =
        {
            "WINFUT",
            "WDOFUT"
        };

        public string Url { get; set; } =
            "https://localhost:5002/api/datahub";

        private readonly ConcurrentQueue<string> _winBuffer =
            new ConcurrentQueue<string>();

        private readonly ConcurrentQueue<string> _wdoBuffer =
            new ConcurrentQueue<string>();

        private readonly ConcurrentQueue<string> _otherBuffer =
            new ConcurrentQueue<string>();

        // CONFIGURACAO DOS T&Ts
        public ObservableCollection<TntItem> Tnts { get; set; } =
            new ObservableCollection<TntItem>
            {
                new TntItem
                {
                    TNTSymbol = "T&T0",
                    Symbol = "WINFUT"
                },

                new TntItem
                {
                    TNTSymbol = "T&T1",
                    Symbol = "WDOFUT"
                }
            };

        // LOGS SEPARADOS
        public ObservableCollection<string> WinMessages { get; set; } =
            new ObservableCollection<string>();

        public ObservableCollection<string> WdoMessages { get; set; } =
            new ObservableCollection<string>();

        public ObservableCollection<string> OtherMessages { get; set; } =
            new ObservableCollection<string>();

        BackgroundWorker workerTnT =
            new BackgroundWorker();

        CancellationTokenSource SourceTnT { get; set; }

        CancellationToken TokenTnT { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            DispatcherTimer timer =
            new DispatcherTimer();

            timer.Interval =
                TimeSpan.FromMilliseconds(100);

            timer.Tick += FlushUi;

            timer.Start();
        }

        private void FlushUi(
            object sender,
            EventArgs e)
        {
            FlushQueue(
                _winBuffer,
                WinMessages);

            FlushQueue(
                _wdoBuffer,
                WdoMessages);

            FlushQueue(
                _otherBuffer,
                OtherMessages);
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
                    OtherMessages.Insert(0, ex.ToString());
                });
            }
        }

        private void Print(
            object s,
            ProgressChangedEventArgs ev)
        {
            if (ev.UserState is TickLogItem item)
            {
                switch (item.Symbol)
                {
                    case "WINFUT":

                        _winBuffer.Enqueue(
                            item.Message);

                        break;

                    case "WDOFUT":

                        _wdoBuffer.Enqueue(
                            item.Message);

                        break;

                    default:

                        _otherBuffer.Enqueue(
                            item.Message);

                        break;
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusTextBlock.Text =
                    TimesAndTradesRtd.ConnectionState;

                StatusTextBlockData.Text =
                    $"{ev.ProgressPercentage} Ticks/s";
            }));
        }

        private async void Clear_Click(
            object sender,
            RoutedEventArgs e)
        {
            WinMessages.Clear();
            WdoMessages.Clear();
            OtherMessages.Clear();

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

    public class TntItem
    {
        public string TNTSymbol { get; set; }

        public string Symbol { get; set; }
    }
}
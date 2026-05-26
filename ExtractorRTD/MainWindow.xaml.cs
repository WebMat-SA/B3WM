using ExtractorRTD.Services;
using ExtractorTryd.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
            while (true)
            {
                if (workerTnT.CancellationPending ||
                    SourceTnT.IsCancellationRequested)
                {
                    e.Cancel = true;

                    TimesAndTradesRtd.Stop();

                    break;
                }

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
                        workerTnT);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        OtherMessages.Insert(0, ex.ToString());
                    });
                }

                Task.Delay(1000).Wait();
            }
        }

        private void Print(
    object s,
    ProgressChangedEventArgs ev)
        {
            Dispatcher.Invoke(() =>
            {
                if (ev.UserState is TickLogItem item)
                {
                    switch (item.Symbol)
                    {
                        case "WINFUT":

                            WinMessages.Insert(
                                0,
                                item.Message);

                            while (WinMessages.Count > 30)
                                WinMessages.RemoveAt(30);

                            break;

                        case "WDOFUT":

                            WdoMessages.Insert(
                                0,
                                item.Message);

                            while (WdoMessages.Count > 30)
                                WdoMessages.RemoveAt(30);

                            break;

                        default:

                            OtherMessages.Insert(
                                0,
                                $"[{item.Symbol}] {item.Message}");

                            while (OtherMessages.Count > 30)
                                OtherMessages.RemoveAt(30);

                            break;
                    }
                }
            });

            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text =
                    TimesAndTradesRtd.ConnectionState;
            });

            Dispatcher.Invoke(() =>
            {
                StatusTextBlockData.Text =
                    $"{ev.ProgressPercentage} Ticks/s";
            });
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
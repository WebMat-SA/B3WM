using Microsoft.AspNetCore.SignalR.Client;
using ExtractorTryd.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ExtractorTryd
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string Symbol { get; set; } = "WINJ26";
        public string Url { get; set; } = "https://localhost:5001/api/datahub";

        BackgroundWorker workerTnT = new BackgroundWorker();
        CancellationTokenSource SourceTnT { get; set; }
        CancellationToken TokenTnT { get; set; }


        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;
        }

        private void TimesAndTrades_Click(object sender, RoutedEventArgs e)
        {
            if (!workerTnT.IsBusy || SourceTnT.IsCancellationRequested)
            {

                workerTnT = new BackgroundWorker();

                workerTnT.WorkerReportsProgress = true;
                workerTnT.WorkerSupportsCancellation = true;

                workerTnT.DoWork += Work;
                workerTnT.ProgressChanged += Print;

                SourceTnT = new CancellationTokenSource();
                TokenTnT = SourceTnT.Token;

                workerTnT.RunWorkerAsync();
            }
            else
            {
                SourceTnT.Cancel();
                workerTnT.CancelAsync();
                //TimesAndTrades.StopConnection();
            }
        }

        private void Work (object obj, DoWorkEventArgs e)
        {
            while(true)
            {
                if (workerTnT.CancellationPending || SourceTnT.IsCancellationRequested)
                {
                    e.Cancel = true;
                    break;
                }

                TimesAndTrades.Start(TokenTnT, workerTnT,new string[] { Symbol }, Url);

                Task.Delay(1000).Wait();
            }
        }

        private void Print(object s, ProgressChangedEventArgs ev)
        {
            Dispatcher.Invoke(() =>
            {
                MessagesListBox.Items.Add($"{ev.UserState.ToString()}");

                if (MessagesListBox.Items.Count > 15) MessagesListBox.Items.RemoveAt(0);
            });

            Dispatcher.Invoke(() => StatusTextBlock.Text = $"{TimesAndTrades.hubConnection?.State.ToString()}");
            Dispatcher.Invoke(() => StatusTextBlockData.Text = $"{ev.ProgressPercentage}");
        }

        private async void Clear_TnT_Click(object sender, RoutedEventArgs e)
        {
            if (TimesAndTrades.hubConnection == null || TimesAndTrades.hubConnection.State != HubConnectionState.Connected)
                TimesAndTrades.StartHubConnection();

            await TimesAndTrades.hubConnection.SendAsync("SendDataTnT", Encoding.ASCII.GetBytes($"Texto{new Random().Next(100,999)}"));

            TimesAndTrades.Counter = 0;
        }

        private void Clear_Book_Click(object sender, RoutedEventArgs e)
        {
            Book.Counter = 0;
        }

    }
}

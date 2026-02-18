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

        BackgroundWorker workerTnT = new BackgroundWorker();
        CancellationTokenSource SourceTnT { get; set; }
        CancellationToken TokenTnT { get; set; }


        BackgroundWorker workerBk = new BackgroundWorker();
        CancellationTokenSource SourceBk { get; set; }
        CancellationToken TokenBk { get; set; }

        BackgroundWorker workerTntS = new BackgroundWorker();
        CancellationTokenSource SourceTntS { get; set; }
        CancellationToken TokenTntS { get; set; }


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

                TimesAndTrades.Start(TokenTnT, workerTnT,new string[] { Symbol });

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

        private void Clear_TnT_Click(object sender, RoutedEventArgs e)
        {
            TimesAndTrades.Counter = 0;
        }


        private void Book_Click(object sender, RoutedEventArgs e)
        {
            if (!workerBk.IsBusy || SourceBk.IsCancellationRequested)
            {
                workerBk = new BackgroundWorker();
                workerBk.WorkerReportsProgress = true;
                workerBk.WorkerSupportsCancellation = true;
                workerBk.DoWork += WorkBk;
                workerBk.ProgressChanged += PrintBk;
                SourceBk = new CancellationTokenSource();
                TokenBk = SourceBk.Token;
                workerBk.RunWorkerAsync();
            }
            else
            {
                SourceBk.Cancel();
                workerBk.CancelAsync();
                //Book.StopConnection();
            }
        }

        private void WorkBk(object obj, DoWorkEventArgs e)
        {
            while (true)
            {
                if (workerBk.CancellationPending || SourceBk.IsCancellationRequested)
                {
                    e.Cancel = true;
                    break;
                }
                Book.Start(TokenBk, workerBk, new string[] { Symbol });
                Task.Delay(1000).Wait();
            }
        }

        private void PrintBk(object s, ProgressChangedEventArgs ev)
        {
            Dispatcher.Invoke(() =>
            {
                MessagesListBoxBk.Items.Add($"{ev.UserState.ToString()}");
                if (MessagesListBoxBk.Items.Count > 15) MessagesListBoxBk.Items.RemoveAt(0);
            });

            Dispatcher.Invoke(() => StatusTextBlockBk.Text = $"{Book.hubConnection.State.ToString()}");
            Dispatcher.Invoke(() => StatusTextBlockDataBk.Text = $"{ev.ProgressPercentage}");
        }

        private void Clear_Book_Click(object sender, RoutedEventArgs e)
        {
            Book.Counter = 0;
        }


        private void TntS_Click(object sender, RoutedEventArgs e)
        {
            if (!workerTntS.IsBusy || SourceTntS.IsCancellationRequested)
            {
                workerTntS = new BackgroundWorker();
                workerTntS.WorkerReportsProgress = true;
                workerTntS.WorkerSupportsCancellation = true;
                workerTntS.DoWork += WorkTntS;
                workerTntS.ProgressChanged += PrintTntS;
                SourceTntS = new CancellationTokenSource();
                TokenTntS = SourceTntS.Token;
                workerTntS.RunWorkerAsync();
            }
            else
            {
                SourceTntS.Cancel();
                workerTntS.CancelAsync();
                //TimesAndTradesSimple.StopConnection();
            }
        }

        private void WorkTntS(object obj, DoWorkEventArgs e)
        {
            while (true)
            {
                if (workerTntS.CancellationPending || SourceTntS.IsCancellationRequested)
                {
                    e.Cancel = true;
                    break;
                }
                TimesAndTradesSimple.Start(TokenTntS, workerTntS, new string[] { Symbol });
                Task.Delay(1000).Wait();
            }
        }

        private void PrintTntS(object s, ProgressChangedEventArgs ev)
        {
            Dispatcher.Invoke(() =>
            {
                MessagesListBoxTntS.Items.Add($"{ev.UserState.ToString()}");
                if (MessagesListBoxTntS.Items.Count > 15) MessagesListBoxTntS.Items.RemoveAt(0);
            });

            Dispatcher.Invoke(() => StatusTextBlockTntS.Text = $"{TimesAndTradesSimple.hubConnection.State.ToString()}");
            Dispatcher.Invoke(() => StatusTextBlockDataTntS.Text = $"{ev.ProgressPercentage}");
        }

        private void Clear_TntS_Click(object sender, RoutedEventArgs e)
        {
            TimesAndTradesSimple.Counter = 0;
        }


    }
}

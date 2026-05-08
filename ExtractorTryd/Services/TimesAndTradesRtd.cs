using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ExtractorTryd.Services
{
    /// <summary>
    /// Leitor RTD via COM (esqueleto genérico).
    /// - Projetado para funcionar com servidores RTD COM que exponham métodos de subscribe/get
    /// - Configure o progId do servidor COM e o nome dos métodos quando necessário
    ///</summary>
    public class TimesAndTradesRtd
    {
        public static int Counter { get; set; } = 0;

        public static string[] ativos { get; set; } = new string[] { "WINJ26" };

        // ProgID do servidor COM RTD (ex.: "Profit.RTD" — ajustar conforme o servidor instalado)
        public static string ProgId { get; set; }

        // Poll interval quando o servidor não empurra eventos
        public static int PollIntervalMs { get; set; } = 50;

        private static object _comServer;
        private static Type _comType;

        public static readonly Channel<byte[]> _channelToDo =
            Channel.CreateBounded<byte[]>(new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                SingleWriter = true
            });

        public static void StartComConnection()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ProgId))
                {
                    _comType = Type.GetTypeFromProgID(ProgId);

                    if (_comType != null)
                    {
                        if (_comServer != null)
                        {
                            try { Marshal.ReleaseComObject(_comServer); } catch { }
                            _comServer = null;
                        }

                        _comServer = Activator.CreateInstance(_comType);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartComConnection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Trabalho que consulta o servidor RTD e empurra mensagens para o canal
        /// </summary>
        public static async Task WorkChannel()
        {
            while (true)
            {
                StartComConnection();

                try
                {
                    while (await _channelToDo.Reader.WaitToReadAsync())
                    {
                        int count = 0;

                        using (var ms = new System.IO.MemoryStream())
                        {
                            while (_channelToDo.Reader.TryRead(out var data) && count < 10)
                            {
                                ms.Write(data, 0, data.Length);
                                count++;
                            }

                            // Aqui o envio fica a cargo de quem consumir o canal. Mantive o padrão de escrever batches
                            // para compatibilidade com o TimesAndTrades original.
                            // No projeto original essas batches eram enviadas por SignalR; integre conforme necessário.
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public static void Start(CancellationToken stoppingToken, BackgroundWorker worker, string[] _ativos, string progId, int pollIntervalMs = 50)
        {
            try
            {
                ativos = _ativos ?? ativos;
                ProgId = progId;
                PollIntervalMs = pollIntervalMs > 0 ? pollIntervalMs : PollIntervalMs;

                StartComConnection();

                _ = WorkChannel();

                // Se o servidor COM expõe método de subscrição, tente inscrever os símbolos
                TrySubscribeAll();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Polling genérico: tenta obter valores por símbolo usando métodos comuns
                    foreach (var symbol in ativos)
                    {
                        try
                        {
                            var value = TryGetValue(symbol);

                            if (value != null)
                            {
                                // Monta payload texto terminando em # para compatibilidade com processamento existente
                                string payload = $"{symbol}|{DateTime.UtcNow:o}|{value}#";
                                var bytes = Encoding.ASCII.GetBytes(payload);
                                _channelToDo.Writer.TryWrite(bytes);

                                Counter++;
                                string textData = $"RTD {symbol} {value}";
                                worker?.ReportProgress(Counter, textData);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading {symbol}: {ex.Message}");
                        }

                        Thread.Sleep(PollIntervalMs);
                    }
                }

                TryUnsubscribeAll();
            }
            catch (Exception expt)
            {
                Console.WriteLine(expt.Message);
            }

            Thread.Sleep(10000);
        }

        private static object TryGetValue(string symbol)
        {
            if (_comServer == null) return null;

            // Tenta vários nomes de métodos/assinaturas comumente usados em servidores COM RTD
            var methodCandidates = new[] { "GetLast", "GetLastValue", "GetValue", "Value", "Get" };

            foreach (var name in methodCandidates)
            {
                try
                {
                    var mi = _comType.GetMethod(name);
                    if (mi != null)
                    {
                        return mi.Invoke(_comServer, new object[] { symbol });
                    }
                }
                catch { }
            }

            // Se o servidor implementar um indexador padrão (default member), podemos tentar via late binding
            try
            {
                dynamic dyn = _comServer;
                try
                {
                    var v = dyn[symbol];
                    return v;
                }
                catch { }
            }
            catch { }

            return null;
        }

        private static void TrySubscribeAll()
        {
            if (_comServer == null) return;

            foreach (var s in ativos)
            {
                TryInvokeMethodIfExists("Subscribe", s);
                TryInvokeMethodIfExists("SubscribeSymbol", s);
                TryInvokeMethodIfExists("AddSymbol", s);
            }
        }

        private static void TryUnsubscribeAll()
        {
            if (_comServer == null) return;

            foreach (var s in ativos)
            {
                TryInvokeMethodIfExists("Unsubscribe", s);
                TryInvokeMethodIfExists("RemoveSymbol", s);
            }

            try { Marshal.ReleaseComObject(_comServer); } catch { }
            _comServer = null;
        }

        private static void TryInvokeMethodIfExists(string methodName, params object[] args)
        {
            if (_comServer == null || _comType == null) return;

            try
            {
                var mi = _comType.GetMethod(methodName);
                if (mi != null)
                {
                    mi.Invoke(_comServer, args);
                }
                else
                {
                    // tenta via dynamic para métodos late-bound
                    try
                    {
                        dynamic dyn = _comServer;
                        // Invoca se existir
                        ((object)dyn).GetType().GetMethod(methodName)?.Invoke(dyn, args);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}

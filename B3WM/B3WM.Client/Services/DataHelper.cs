using B3WM.Client.Pages;
using B3WM.Shared.Entity;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;


namespace B3WM.Client.Services
{
    public class DataHelper
    {
        private static readonly char[] PaperSeparator = new[] { '#' };
        private static readonly char[] FieldSeparator = new[] { '|' };
        private static readonly char[] ItemSeparator = new[] { '@' };
        private static readonly char[] BookItemSeparator = new[] { ';' };
        private const string BuyerStarter = "Comprador";
        private const string SellerStarter = "Vendedor";
        private const string CrossStarter = "Cross";
        private const string OpenBookValue = "Aber.";

        /// <summary>Valores e preços vêm no formato brasileiro: ponto = separador de milhares (186.565 = 186565).</summary>
        private static readonly CultureInfo BrazilianNumberFormat = CultureInfo.GetCultureInfo("pt-BR");

        //private readonly byte[] _data;
        private readonly string _textData;
        private static int _timesAndTradesSequence = 0;
        private static int _bookSequence = 0;

        public DataHelper(string data)
        {
            _textData = data;
        }

        /// <summary>
        /// Convesão do TimesAndTrades vindos do RTD/DDE
        /// </summary>
        /// <param name="isSimpleTrade">Se estiver usando a opção de trades simples (somente o ultimo trade do rtd) então marcar isso como verdadeiro</param>
        /// <returns>Uma Lista de Ticks2 já convertidos</returns>
        public ICollection<Ticks2> TimesAndTrades(bool isSimpleTrade = false)
        {
            var sw = Stopwatch.StartNew();
            bool cacheHit = false;
            List<Ticks2> ticksQueue;

            ticksQueue = ParseTimesAndTrades(isSimpleTrade);

            sw.Stop();
            HelperPerformanceConfig.Log(
                nameof(DataHelper),
                "TimesAndTrades",
                sw.ElapsedMilliseconds,
                $"payloadBytes={_textData.Length} ticks={ticksQueue.Count} cacheHit={cacheHit}");
            return ticksQueue;
        }

        private List<Ticks2> ParseTimesAndTrades(bool isSimpleTrade = false)
        {
            var ticksQueue = new List<Ticks2>();
            var separator = !isSimpleTrade ? "NEGS!" : "NEG!";
            //string textData = Encoding.UTF8.GetString(_data);
            string textData = _textData;
            string[] manyPapersInfo = textData.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            foreach (string onePaperInfo in manyPapersInfo)
            {
                string[] parameters = onePaperInfo.Split(FieldSeparator, StringSplitOptions.None);
                if (parameters.Length < 7)
                    continue;

                string[] trydIds = parameters[1].Split(ItemSeparator, StringSplitOptions.None);
                string[] times = parameters[2].Split(ItemSeparator, StringSplitOptions.None);
                string[] values = parameters[3].Split(ItemSeparator, StringSplitOptions.None);
                string[] volumes = parameters[4].Split(ItemSeparator, StringSplitOptions.None);
                string[] buyers = parameters[5].Split(ItemSeparator, StringSplitOptions.None);
                string[] sellers = parameters[6].Split(ItemSeparator, StringSplitOptions.None);
                string[]? starters = parameters.Length > 7 ? parameters[7].Split(ItemSeparator, StringSplitOptions.None) : null;

                int lines = Math.Min(
                    trydIds.Length,
                    Math.Min(times.Length,
                    Math.Min(values.Length,
                    Math.Min(volumes.Length,
                    Math.Min(buyers.Length, sellers.Length)))));

                for (int i = 0; i < lines; i++)
                {
                    if (!int.TryParse(trydIds[i], out int trydId))
                        continue;

                    if (!DateTime.TryParseExact(times[i], "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                        continue;

                    if (!double.TryParse(values[i], NumberStyles.Any, BrazilianNumberFormat, out double value))
                        continue;

                    if (!int.TryParse(volumes[i], out int volume))
                        continue;

                    if (!int.TryParse(buyers[i], out int buyerInt))
                        continue;

                    if (!int.TryParse(sellers[i], out int sellerInt))
                        continue;

                    var tick = new Ticks2
                    {
                        TrydID = trydId,
                        Time = time,
                        Value = value,
                        Volume = volume,
                        Buyer = (Ticks2.Agents)buyerInt,
                        Seller = (Ticks2.Agents)sellerInt,
                        Starter = ParseStarter(starters, i),
                        Symbol = parameters[0]
                    };

                    ticksQueue.Add(tick);
                }
            }

            return ticksQueue;
        }

        /// <summary>
        /// Convesão do Book de ofertas vindos do RTD/DDE
        /// </summary>
        /// <returns>Uma Lista de BookItem já convertidos</returns>
        public List<BookItem> Book()
        {
            var sw = Stopwatch.StartNew();
            List<BookItem> pseudoBook = new List<BookItem>();

            string[] manyPapersInfo = _textData.Split(PaperSeparator, StringSplitOptions.RemoveEmptyEntries);



            for (int paperinfoCount = manyPapersInfo.Length - 1; paperinfoCount >= 0; paperinfoCount--)
            {
                string onePaperInfo = manyPapersInfo[paperinfoCount];

                if (string.IsNullOrEmpty(onePaperInfo))
                    continue;

                string[] parameters = onePaperInfo.Split('|');
                if (parameters.Length <= 1)
                    continue;

                try
                {
                    // iterate book parameters and split each only once
                    BookItem bi = null;
                    for (int bookParam = 1; bookParam < parameters.Length; bookParam++)
                    {
                        string[] bookitem = parameters[bookParam].Split(BookItemSeparator);

                        if (bookitem.Length != 4)
                            continue;

                        if (!int.TryParse(bookitem[2], out int coluna))
                            continue;

                        string content = bookitem[3];

                        switch (coluna)
                        {
                            case 0:
                                if (!int.TryParse(content, out int agent0))
                                    continue;

                                bi = new BookItem();
                                bi.Agent = (Ticks2.Agents)agent0;
                                break;
                            case 1:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int volume1))
                                    continue;

                                bi.Volume = volume1;
                                break;
                            case 2:
                                if (bi == null)
                                    bi = new BookItem();

                                if (content == OpenBookValue)
                                    bi.Value = -1.0d;
                                else if (double.TryParse(content, NumberStyles.Any, BrazilianNumberFormat, out double val2))
                                    bi.Value = val2;
                                else
                                    continue;

                                bi.Type = Ticks2.ActionType.Buy;
                                pseudoBook.Add(bi);
                                bi = null;
                                break;
                            case 3:
                                if (bi == null)
                                    bi = new BookItem();

                                if (content == OpenBookValue)
                                    bi.Value = -1.0d;
                                else if (double.TryParse(content, NumberStyles.Any, BrazilianNumberFormat, out double val3))
                                    bi.Value = val3;
                                else
                                    continue;

                                break;
                            case 4:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int volume4))
                                    continue;

                                bi.Volume = volume4;
                                break;
                            case 5:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int agent5))
                                    continue;

                                bi.Agent = (Ticks2.Agents)agent5;
                                bi.Type = Ticks2.ActionType.Sale;

                                pseudoBook.Add(bi);
                                bi = null;
                                break;
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine("BookService - " + err.Message);
                    Console.WriteLine(onePaperInfo);
                }
            }

            sw.Stop();
            HelperPerformanceConfig.Log(
                nameof(DataHelper),
                "Book",
                sw.ElapsedMilliseconds,
                $"payloadBytes={_textData.Length} papers={manyPapersInfo.Length} items={pseudoBook.Count}");
            return pseudoBook;
        }

        private static Ticks2.ActionType ParseStarter(string[]? starters, int index)
        {
            if (starters == null || starters.Length == 0)
                return Ticks2.ActionType.Auction;

            string starterValue = index < starters.Length ? starters[index] : starters[0];

            if (starterValue == BuyerStarter) return Ticks2.ActionType.Buy;
            if (starterValue == SellerStarter) return Ticks2.ActionType.Sale;
            if (starterValue == CrossStarter) return Ticks2.ActionType.Cross;
            return Ticks2.ActionType.Auction;
        }



        public static async IAsyncEnumerable<Ticks2> ParseTicks2FromCsv(
            Stream csvStream,
            DateTime date,
            string symbol,
            TimeSpan? StartAtTick = null)
        {
            //var list = new List<Ticks2>();

            using var reader = new StreamReader(csvStream, Encoding.UTF8);

            // Ignora primeira linha (título)
            await reader.ReadLineAsync();

            // Ignora cabeçalho
            await reader.ReadLineAsync();

            string? line;
            int counter = 0;

            while ((line = await reader.ReadLineAsync()) != null)
            {

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = ParseCsvLine(line);

                if (parts.Length < 7)
                    continue;

                var tick = new Ticks2
                {
                    Time = date.Date + TimeSpan.Parse(parts[0]),

                    Volume = int.Parse(parts[1].Replace(".","")),

                    Value = double.Parse(
                        parts[2],
                        CultureInfo.GetCultureInfo("pt-BR")
                    ),

                    TrydID = int.Parse(parts[3]),

                    Buyer = ParseAgent(parts[4]),
                    Seller = ParseAgent(parts[5]),

                    Starter = ParseActionType(parts[6]),

                    Symbol = symbol
                };

                counter++;
                HelperPerformanceConfig.Log(nameof(Import), "Enqueue CSV", counter, $"{tick.ToString()}");

                //jumpa para o filtro de tempo
                if (StartAtTick != null && tick.Time.TimeOfDay > StartAtTick)
                    continue;

                yield return tick;
            }

            //return list;
        }

        private static string[] ParseCsvLine(string line)
        {
            return line
                .Split("\",\"")
                .Select(p => p.Trim('"'))
                .ToArray();
        }

        private static Ticks2.Agents ParseAgent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            // Ex: "093-Nova Futura"
            var codeStr = value.Split('-')[0];

            if (int.TryParse(codeStr, out int code) &&
                Enum.IsDefined(typeof(Ticks2.Agents), code))
            {
                return (Ticks2.Agents)code;
            }

            return 0; // desconhecido
        }

        private static Ticks2.ActionType ParseActionType(string value)
        {
            return value[0] switch
            {
                'C' => Ticks2.ActionType.Buy,
                'V' => Ticks2.ActionType.Sale,
                'L' => Ticks2.ActionType.Auction,
                'D' => Ticks2.ActionType.Cross,
                'R' => Ticks2.ActionType.RLP,
                _ => 0
            };
        }
    }
}

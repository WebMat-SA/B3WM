using B3WM.Shared.Entity;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace B3WM.Client.Services
{
    public class DataHelper
    {

        /// <summary>Valores e preços vêm no formato brasileiro: ponto = separador de milhares (186.565 = 186565).</summary>
        private static readonly CultureInfo BrazilianNumberFormat = CultureInfo.GetCultureInfo("pt-BR");


        byte[]? data;
        public DataHelper(byte[] data)
        {
            this.data = data;
        }

        public ICollection<Ticks2> TimesAndTrades(bool isSimpleTrade = false)
        {
            string prefix = isSimpleTrade ? "NEG!" : "NEGS!";

            ICollection<Ticks2> TicksQueue = new Collection<Ticks2>();

            string textData = Encoding.UTF8.GetString(data);

            string[] manyPapersInfo = (textData).Split(new[] { "#" }, StringSplitOptions.None);

            foreach (string onePaperInfo in manyPapersInfo)
            {
                if (string.IsNullOrEmpty(onePaperInfo))
                    continue;

                string[] parameters = onePaperInfo.Split(new[] { "|" }, StringSplitOptions.None);
                if (parameters.Length >= 7)
                {
                    // pre-split columns once to avoid repeated allocations
                    var col0 = parameters[0].Split(new[] { prefix }, StringSplitOptions.None);
                    string paper = col0.Length > 1 ? col0[1] : col0[0];

                    string[] trydIds = parameters[1].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] times = parameters[2].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] values = parameters[3].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] volumes = parameters[4].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] buyers = parameters[5].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] sellers = parameters[6].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] starters = parameters.Length > 7 ? parameters[7].Split(new[] { "@" }, StringSplitOptions.None) : null;

                    int lines = trydIds.Length;

                    for (int i = 0; i < lines; i++)
                    {
                        try
                        {
                            // parse fields using TryParse to avoid exceptions
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
                                Seller = (Ticks2.Agents)sellerInt
                            };

                            if (starters != null && starters.Length > 0)
                            {
                                string starterValue = (i < starters.Length) ? starters[i] : starters[0];
                                tick.Starter = starterValue == "Comprador" ? Ticks2.ActionType.Buy : (starterValue == "Vendedor" ? Ticks2.ActionType.Sale : (starterValue == "Cross" ? Ticks2.ActionType.Cross : Ticks2.ActionType.Auction));
                            }
                            else
                            {
                                tick.Starter = Ticks2.ActionType.Auction;
                            }

                            TicksQueue.Add(tick);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine("TimesAndSales - " + err.Message);
                            Console.WriteLine(onePaperInfo);
                        }
                    }
                }
            }

            return TicksQueue;
        }

        public List<BookItem> Book()
        {
            List<BookItem> PseudoBook = new List<BookItem>();

            string[] manyPapersInfo = (Encoding.UTF8.GetString(data)).Split('#');

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
                    string paper = parameters[0].Replace("LVL2!", "").ToUpper();

                    // iterate book parameters and split each only once
                    BookItem bi = null;
                    for (int bookParam = 1; bookParam < parameters.Length; bookParam++)
                    {
                        string[] bookitem = parameters[bookParam].Split(';');

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

                                if (content == "Aber.")
                                    bi.Value = -1.0d;
                                else if (double.TryParse(content, NumberStyles.Any, BrazilianNumberFormat, out double val2))
                                    bi.Value = val2;
                                else
                                    continue;

                                bi.Type = Ticks2.ActionType.Buy;
                                PseudoBook.Add(bi);
                                bi = null;
                                break;
                            case 3:
                                if (bi == null)
                                    bi = new BookItem();

                                if (content == "Aber.")
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

                                PseudoBook.Add(bi);
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

            return PseudoBook;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public static class Speech
    {
        private static int GroupedMoreThan { get; set; } = 5;
        private static IDictionary<BindType, string> DefaultSpeechs = new Dictionary<BindType, string>
        {
            { BindType.AuctionUp, "Ativo {paper} está a {value} {unit} do leilão de alta."},
            { BindType.AuctionDown, "Ativo {paper} está a {value} {unit} do leilão de baixa."},

            { BindType.AuctionIn, "Ativo {paper} entrou em leilão."},
            { BindType.AuctionOut, "Ativo {paper} saiu do leilão."},

            { BindType.SpreadUp, "Ativo {paper} tem spread maior que {value} {unit}."},

            { BindType.TicksQuantityAbove, "Ativo {paper} tem número de negociações maior que {value}."},

            { BindType.V1MinAbove, "Ativo {paper} tem volátilidade relativa ao ultimo minuto maior que {value}."},

            { BindType.DistortionUp, "Ativo {paper} tem distorção à V.WAP maior que {value} {unit}."},
            { BindType.DistortionDown, "Ativo {paper} tem distorção à V.WAP menor que {value} {unit}."},

            { BindType.AuctionTimeRemoved, "Ativo {paper} removeu relógio de leilão."},

            { BindType.AgentParticipating, "Ativo {paper} tem agentes {agent} com participação em negociações acima de {value} {unit}"},

            { BindType.SequentialActions, "Ativo {paper} tem agentes {agent} com ações sequenciais de {unit} às {time}"},
            { BindType.NotoriousVolume, "Ativo {paper} tem Volume acima de {value} {unit} da média."},

            { BindType.MovimentShaker, "Ativo {paper} tem agente {agent} com padrão de movimentação."},

            { BindType.Variation, "Ativo {paper} tem variação acima de {value} {unit}."},

            { BindType.OfferRenewalVolume, "Ativo {paper} tem agente {agent} renovando {volume} ações à {value} {unit}"},

            { BindType.MarketClose, "Atenção. {value} minutos para fechamento do mercado"},
        };
        private static IDictionary<BindType, string> GroupedSpeechs = new Dictionary<BindType, string>
        {
            { BindType.AuctionUp, "{quantity} Ativos estão a próximos do leilão de alta."},
            { BindType.AuctionDown, "{quantity} Ativos estão a próximos do leilão de baixa."},

            { BindType.AuctionIn, "{quantity} Ativos entraram em leilão."},
            { BindType.AuctionOut, "{quantity} Ativos sairam do leilão."},

            { BindType.SpreadUp, "{quantity} Ativos tem spread considerável."},

            { BindType.TicksQuantityAbove, "{quantity} Ativos tem número de negociações considerável."},

            { BindType.V1MinAbove, "{quantity} Ativos tem volátilidade relativa ao ultimo minuto considerável."},

            { BindType.DistortionUp, "{quantity} Ativos tem distorção à V.WAP considerável."},
            { BindType.DistortionDown, "{quantity} Ativos tem distorção à V.WAP considerável."},

            { BindType.AuctionTimeRemoved, "{quantity} Ativos removeram relógio de leilão."},

            { BindType.AgentParticipating, "{quantity} Ativos tem agentes com participação considerável."},
            { BindType.NotoriousVolume, "{quantity} Ativos tem volume acima da média."},

            { BindType.SequentialActions, "{quantity} Ativos tem agentes com ações sequenciais consideráveis."},

            { BindType.MovimentShaker, "{quantity} Ativos tem agentes com ações padrão de movimentação."},

            { BindType.OfferRenewalVolume, "{quantity} Ativos tem renovações consideráveis."},
        };

        public static IList<string> GetSpeechText(ICollection<Alerts> alerts, IList<TimesAndSales> data, ref IDictionary<string, IList<AlertBindType>> SpeechControl)
        {
            IList<string> result = new List<string>();
            IDictionary<BindType, List<string>> temp = new Dictionary<BindType, List<string>>();

            //para cada papel que chegou do servidor
            foreach (var tns in data)
            {
                // adicona o simbolo no no controle, caso não exista
                if (!SpeechControl.ContainsKey(tns.Customer.Symbol))
                {
                    var auxList = new List<AlertBindType>();

                    if (alerts.FirstOrDefault(q => q.IsActive && q.Bind == BindType.AuctionIn) != null)
                        auxList.Add(new AlertBindType(BindType.AuctionIn,alerts.FirstOrDefault(q => q.IsActive && q.Bind == BindType.AuctionIn).AlertsID));

                    if (alerts.FirstOrDefault(q => q.IsActive && q.Bind == BindType.AuctionOut) != null)
                        auxList.Add(new AlertBindType(BindType.AuctionOut,alerts.FirstOrDefault(q => q.IsActive && q.Bind == BindType.AuctionOut).AlertsID));


                    SpeechControl.Add(tns.Customer.Symbol, auxList);
                }


                //cada alerta
                foreach (var alert in alerts.Where(q => q.IsActive).OrderByDescending(q => q.Bind))
                {
                    switch (alert.Bind)
                    {
                        case BindType.AuctionDown:
                            bool valueCheck = false;
                            if (tns.Last > 0)
                            {
                                if (alert.UnitType == UnitsType.Value)
                                    valueCheck = Math.Abs(tns.ValueAuctionDown - tns.Last) <= alert.Value;
                                else if (alert.UnitType == UnitsType.Percent)
                                    valueCheck = Math.Abs(tns.ValueAuctionDown - tns.Last) <= alert.Value * (tns.FirstValueAfterAuction * 0.1d);

                                if (valueCheck && !SpeechControl[tns.Customer.Symbol].Select(q=>q.AlertID).Contains(alert.AlertsID) && !tns.IsInAuction)
                                {
                                    SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID ));
                                    AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, (alert.UnitType == UnitsType.Percent ? alert.Value * 100.0 : alert.Value), alert.UnitType));
                                }
                                else if (!valueCheck && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                                {
                                    SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q=>q.AlertID == alert.AlertsID));
                                }
                            }

                            break;

                        case BindType.AuctionUp:
                            valueCheck = false;
                            if (tns.Last > 0)
                            {
                                if (alert.UnitType == UnitsType.Value)
                                    valueCheck = Math.Abs(tns.ValueAuctionUp - tns.Last) <= alert.Value;
                                else if (alert.UnitType == UnitsType.Percent)
                                    valueCheck = Math.Abs(tns.ValueAuctionUp - tns.Last) <= alert.Value * (tns.FirstValueAfterAuction * 0.1d);

                                if (valueCheck && !SpeechControl[tns.Customer.Symbol].Select(q=>q.AlertID).Contains(alert.AlertsID) && !tns.IsInAuction)
                                {
                                    SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                    AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, (alert.UnitType == UnitsType.Percent ? alert.Value * 100.0 : alert.Value), alert.UnitType));
                                }
                                else if (!valueCheck && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                                {
                                    SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                                }
                            }
                            break;

                        case BindType.AuctionIn:
                        case BindType.AuctionOut:

                            if ((alert.Bind == BindType.AuctionIn ? tns.IsInAuction : !tns.IsInAuction) && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol));
                            }
                            else if ((alert.Bind == BindType.AuctionIn ? !tns.IsInAuction : tns.IsInAuction) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }
                            break;

                        case BindType.DistortionUp:
                        case BindType.DistortionDown:
                            bool valueCheckDistortion = false;

                            //adicionar condição de tempo intraday da condição de distorção

                            if (alert.UnitType == UnitsType.Value && alert.Bind == BindType.DistortionDown)
                                valueCheckDistortion = tns.Distortion <= alert.Value;
                            else if (alert.UnitType == UnitsType.Value && alert.Bind == BindType.DistortionUp)
                                valueCheckDistortion = tns.Distortion >= alert.Value;
                            else if (alert.UnitType == UnitsType.Percent && alert.Bind == BindType.DistortionDown)
                                valueCheckDistortion = tns.DistortionPercent <= alert.Value;
                            else if (alert.UnitType == UnitsType.Percent && alert.Bind == BindType.DistortionUp)
                                valueCheckDistortion = tns.DistortionPercent >= alert.Value;

                            if (valueCheckDistortion && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID ));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, alert.Value, alert.UnitType));
                            }
                            else if (!valueCheckDistortion && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;

                        case BindType.SpreadUp:
                            if ((double)tns.Spread >= alert.Value && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind,  alert.AlertsID ));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, alert.Value, alert.UnitType));
                            }
                            else if (!((double)tns.Spread >= alert.Value) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;

                        case BindType.TicksQuantityAbove:

                            if (tns.TicksCount != null)
                            {
                                if (tns.TicksCount.Value >= alert.Value && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                                {
                                    SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                    AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol));
                                }
                                else if (!(tns.TicksCount >= alert.Value) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                                {
                                    SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                                }
                            }

                            break;

                        case BindType.V1MinAbove:

                            if ((double)tns.Volatility1Min >= alert.Value && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID ));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, alert.Value, alert.UnitType));
                            }
                            else if (!((double)tns.Volatility1Min >= alert.Value) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;

                        case BindType.AuctionTimeRemoved:

                            if (tns.IsInAuction && (string.IsNullOrEmpty(tns.TimeToEndsAuction.Trim()) || tns.TimeToEndsAuction.Trim() == "") && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol));
                            }
                            else if ((!tns.IsInAuction || (tns.IsInAuction && !string.IsNullOrEmpty(tns.TimeToEndsAuction.Trim()) && tns.TimeToEndsAuction.Trim() != "")) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;

                        case BindType.AgentParticipating:

                            if (tns.AgentsParticipating != null && tns.AgentsParticipating.Where(q => q.Value >= alert.Value).Count() > 0 && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol,alert.Value*100.0d,alert.UnitType, tns.AgentsParticipating.Where(q => q.Value >= alert.Value).Select(x=>x.Key).ToArray()));
                            }
                            else if ( (tns.AgentsParticipating == null || tns.AgentsParticipating.Where(q=> q.Value >= alert.Value).Count() <= 0) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }
                            break;
                        case BindType.SequentialActions:

                            if (tns.SequentialAgents != null && tns.SequentialAgents.Any() && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind,alert.AlertsID));

                                var grouped = tns.SequentialAgents.GroupBy(q => q.TypeAction);
                                foreach(var itemGrouped in grouped)
                                {
                                    AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, itemGrouped.Key, itemGrouped.Select(x => x.Agent).ToArray(),itemGrouped.ElementAt(0).Date));
                                }
                            }
                            else if ( (tns.SequentialAgents == null || !tns.SequentialAgents.Any()) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;
                        case BindType.NotoriousVolume:

                            if (tns.VolumeAverage != null && tns.FinancialVolume >= tns.VolumeAverage.Last5Days * alert.Value && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));
                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, alert.Value * 100.0d, alert.UnitType));
                            }
                            else if ((tns.VolumeAverage == null || tns.FinancialVolume < tns.VolumeAverage.Last5Days * alert.Value) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }
                            break;

                        case BindType.MovimentShaker:

                            if (tns.MovimentShaker != null && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));


                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, Ticks2.ActionType.Buy,new Ticks2.Agents[] { tns.MovimentShaker.Agent }));
                            }
                            else if (tns.MovimentShaker == null && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;

                        case BindType.Variation:

                            if (alert.Value*((alert.UnitType == UnitsType.Value) ? 1 : 100) >= ((alert.UnitType == UnitsType.Value) ? tns.Var : tns.VarPercent) && !SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Add(new AlertBindType(alert.Bind, alert.AlertsID));


                                AddTemp(ref temp, alert.Bind, GetSpeechText(alert.Bind, tns.Customer.Symbol, (alert.UnitType == UnitsType.Percent ? alert.Value * 100.0 : alert.Value), alert.UnitType));
                            }
                            else if (alert.Value*((alert.UnitType == UnitsType.Value) ? 1 : 100) < ((alert.UnitType == UnitsType.Value) ? tns.Var : tns.VarPercent) && SpeechControl[tns.Customer.Symbol].Select(q => q.AlertID).Contains(alert.AlertsID))
                            {
                                SpeechControl[tns.Customer.Symbol].Remove(SpeechControl[tns.Customer.Symbol].FirstOrDefault(q => q.AlertID == alert.AlertsID));
                            }

                            break;
                    }
                }
            }

            //check quantities
            CreateSpeechByTemp(ref temp, ref result);

            return result;
        }

        public static IList<string> GetSpeechText(ref ICollection<Reminders> reminders)
        {
            IList<string> result = new List<string>();

            foreach (var item in reminders.OrderBy(q => q.Date))
            {
                if (DateTime.Now >= item.Date)
                {
                    reminders.Remove(item);
                    result.Add(GetSpeechText(item));
                }
            }

            return result;
        }

        public static string GetSpeechText(ref ICollection<Alerts> alerts, DateTime dateBase)
        {
            if (DateTime.Now <= dateBase)
            {
                foreach (var item in alerts.Where(q=>q.IsActive).OrderByDescending(q => q.Value))
                {
                    if (DateTime.Now >= dateBase.AddMinutes(item.Value * -1))
                    {
                        alerts.Remove(item);
                        return GetSpeechText(item.Bind, "", item.Value);
                    }
                }
            }

            return null;
        }

        public static string GetSpeechTextSample(Alerts alert, string paper)
        {
            if (alert.Bind == BindType.SequentialActions || alert.Bind == BindType.MovimentShaker)
                return GetSpeechText(alert.Bind, paper, Ticks2.ActionType.Buy, new Ticks2.Agents[] { Ticks2.Agents.XP });

            return GetSpeechText(alert.Bind, paper, (alert.UnitType == UnitsType.Percent ? alert.Value * 100.0 : alert.Value), alert.UnitType, new Ticks2.Agents[] {Ticks2.Agents.XP});
        }

        // facilitador de conversão de string
        private static string GetSpeechText(BindType bindType, string Paper, double Value, UnitsType type) =>
            DefaultSpeechs[bindType].Replace("{paper}", Paper).Replace("{value} {unit}", (type == UnitsType.Value ? "R$" : "") + Value.ToString("0.##") + (type == UnitsType.Percent ? "%" : "")).Replace("{value}",Value.ToString("0.##"));

        private static string GetSpeechText(BindType bindType, string Paper) =>
            DefaultSpeechs[bindType].Replace("{paper}", Paper);

        private static string GetSpeechText(BindType bindType, string Paper, double Value) =>
            DefaultSpeechs[bindType].Replace("{paper}", Paper).Replace("{value}", Value.ToString("0"));

        private static string GetSpeechText(Reminders reminder) => reminder.Text;

        private static string GetSpeechText(BindType bindType, string Paper, double Value, UnitsType type, Ticks2.Agents[] agents) =>
            GetSpeechText(bindType, Paper, Value, type).Replace("{agent}", string.Join(", ", agents.Select(x => x.ToString())));

        private static string GetSpeechText(BindType bindType, string Paper, Ticks2.ActionType actionType, Ticks2.Agents[] agents) =>
            GetSpeechText(bindType, Paper).Replace("{unit}", Extensions.Enum.GetDescription(actionType)).Replace("{agent}", string.Join(", ", agents.Select(x => x.ToString())));

        private static string GetSpeechText(BindType bindType, string Paper, Ticks2.ActionType actionType, Ticks2.Agents[] agents, DateTime time) => GetSpeechText(bindType, Paper, actionType, agents).Replace("{time}",time.ToString("HH:mm:ss"));

        private static void AddTemp(ref IDictionary<BindType, List<string>> temp, BindType type, string text)
        {
            if (temp.ContainsKey(type))
            {
                temp[type].Add(text);
            }
            else
            {
                temp.Add(type, new List<string>() { text });
            }
        }
        private static void CreateSpeechByTemp(ref IDictionary<BindType, List<string>> temp, ref IList<string> result)
        {
            if (temp != null && temp.Count > 0)
            {

                foreach(var item in temp)
                {
                    if(item.Value.Count >= GroupedMoreThan)
                    {
                        result.Add(GetSpeechText(item.Key, item.Value.Count));
                    }
                    else
                    {
                        foreach(var sp in item.Value)
                        {
                            result.Add(sp);
                        }
                    }
                }
            }
        }
        private static string GetSpeechText(BindType bindType, int quantity) =>
            GroupedSpeechs[bindType].Replace("{quantity}", quantity.ToString("0"));

    }

    public class AlertBindType
    {

        public AlertBindType(BindType _BindType, int _AlertID)
        {
            AlertID = _AlertID;
            BindType = _BindType;
            Date = DateTime.Now;
        }


        public BindType BindType { get; set; }
        public int AlertID { get; set; }
        public DateTime Date { get; set; }
    }
}

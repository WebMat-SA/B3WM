using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class Ticks2
    {

        public int TrydID { get; set; }

        public DateTime Time { get; set; }
        public double Value { get; set; }
        public int Volume { get; set; }
        public Agents Buyer { get; set; }
        public Agents Seller { get; set; }
        public ActionType Starter { get; set; }

        public override string ToString()
        {
            return $"{TrydID}\t {Time.ToString("HH:mm:ss")}\t\t {Value}\t {Volume}\t {Buyer}\t\t\t {Seller}\t\t\t {Starter}";
        }

        public enum Agents
        {
            [Display(Description = "MAGLIANO S.A. CCVM")]
            Magliano = 1,
            [Display(Description = "XP INVESTIMENTOS CCTVM S/A")]
            XP = 3,
            [Display(Description = "ALFA CCVM S.A.")]
            Alfa = 4,
            [Display(Description = "UBS BRASIL CCTVM S/A")]
            UBS = 8,
            [Display(Description = "MERRILL LYNCH S/A CTVM")]
            Merrill = 13,
            [Display(Description = "GUIDE INVESTIMENTOS S.A. CV")]
            Guide = 15,
            [Display(Description = "J. P. MORGAN CCVM S.A.")]
            JP = 16,
            [Display(Description = "BOCOM BBM CCVM S/A")]
            BOCOM = 18,
            [Display(Description = "VOTORANTIM ASSET MANAG. DTVM")]
            Votorantim = 21,
            [Display(Description = "NECTON INVESTIMENTOS S.A. CVMC")]
            Necton = 23,
            [Display(Description = "SANTANDER CCVM S/A")]
            Santander = 27,
            [Display(Description = "UNILETRA CCTVM S.A.")]
            Uniletra = 29,
            [Display(Description = "LEROSA S.A. CVC")]
            Lerosa = 33,
            [Display(Description = "UM INVESTIMENTOS S.A. CTVM")]
            Um = 37,
            [Display(Description = "AGORA CTVM S/A")]
            Agora = 39,
            [Display(Description = "MORGAN STANLEY CTVM S/A")]
            Morgan = 40,
            [Display(Description = "ING CCT S/A")]
            Ing = 41,
            [Display(Description = "CREDIT SUISSE BRASIL S.A. CTVM")]
            Credit = 45,
            [Display(Description = "SOCOPA SC PAULISTA S.A.")]
            Socopa = 58,
            [Display(Description = "SAFRA CVC LTDA.")]
            Safra = 59,
            [Display(Description = "NOVINVEST CVM LTDA.")]
            Novinvest = 63,
            [Display(Description = "BRADESCO S/A CTVM")]
            Bradesco = 72,
            [Display(Description = "COINVALORES CCVM LTDA.")]
            Coinvalores = 74,
            [Display(Description = "CITIGROUP GMB CCTVM S.A.")]
            Citigroup = 77,
            [Display(Description = "MAXIMA S/A CTVM")]
            Maxima = 83,
            [Display(Description = "BTG PACTUAL CTVM S.A.")]
            BTG = 85,
            [Display(Description = "CM CAPITAL MARKETS CCTVM LTDA")]
            Capital = 88,
            [Display(Description = "NUINVEST – TITULO CV S.A.")]
            NuInvest = 90,
            [Display(Description = "RENASCENCA DTVM LTDA.")]
            Renascenca = 92,
            [Display(Description = "NOVA FUTURA CTVM LTDA")]
            Nova_Futura = 93,
            [Display(Description = "MERC. DO BRASIL COR. S.A. CTVM")]
            Mercantil = 106	,
            [Display(Description = "TERRA INVESTIMENTOS DTVM LTDA")]
            Terra = 107	,
            [Display(Description = "SLW CVC LTDA.")]
            SLW = 110	,
            [Display(Description = "ITAU CV S/A")]
            Itau = 114	,
            [Display(Description = "H.COMMCOR DTVM LTDA")]
            HCOMMCOR = 115,
            [Display(Description = "GENIAL INSTITUCIONAL CCTVM S.A")]
            Genial = 120,
            [Display(Description = "BGC LIQUIDEZ DTVM")]
            BGC_Liquidez = 122	,
            [Display(Description = "TULLETT PREBON")]
            Tullet = 127	,
            [Display(Description = "PLANNER CV S.A")]
            Planner = 129	,
            [Display(Description = "FATOR S.A. CV")]
            Fator = 131	,
            [Display(Description = "DIBRAN DTVM LTDA")]
            Dibran = 133	,
            [Display(Description = "ATIVA INVESTIMENTOS S.A. CTCV")]
            Ativa = 147	,
            [Display(Description = "BANRISUL S/A CVMC")]
            Banrisul = 172	,
            [Display(Description = "GENIAL INVESTIMENTOS CVM S.A.")]
            Genial_Invest = 173	,
            [Display(Description = "ELITE CCVM LTDA.")]
            Elite = 174	,
            [Display(Description = "SOLIDUS S/A CCVM")]
            Solidus = 177	,
            [Display(Description = "MUNDINVEST S.A. CCVM")]
            Mundinvest = 181	,
            [Display(Description = "CORRETORA GERAL DE VC LTDA")]
            Geral = 186	,
            [Display(Description = "SITA SCCVM S.A.")]
            Sita = 187	,
            [Display(Description = "SENSO CCVM S.A.")]
            Senso = 191	,
            [Display(Description = "AMARIL FRANKLIN CTV LTDA.")]
            Amaril = 226	,
            [Display(Description = "CODEPE CV E CAMBIO S/A")]
            Codepe = 234	,
            [Display(Description = "GOLDMAN SACHS DO BRASIL CTVM")]
            Goldman = 238	,
            [Display(Description = "BANCO BNP PARIBAS BRASIL S/A")]
            Banco_BNP = 251	,
            [Display(Description = "MIRAE ASSET WEALTH MANAGEMENT")]
            Mirae = 262	,
            [Display(Description = "CLEAR CORRETORA – Grupo XP")]
            Clear = 308	,
            [Display(Description = "BANCO DAYCOVAL")]
            Daycoval = 359	,
            [Display(Description = "RICO INVESTIMENTOS – Grupo XP")]
            Rico = 386	,
            [Display(Description = "BANCO OURINVEST")]
            Banco_Ourinvest = 442	,
            [Display(Description = "BANCO MODAL")]
            Banco_Modal = 683	,
            [Display(Description = "DILLON S.A. DTVM")]
            Dillon = 711,
            [Display(Description = "BB GESTAO DE RECURSOS DTVM S/A")]
            BB_Recursos = 713	,
            [Display(Description = "ICAP DO BRASIL CTVM LTDA")]
            Icap = 735	,
            [Display(Description = "LEV DTVM LTDA")]
            LEV_DTVM = 746,
            [Display(Description = "BB BANCO DE INVESTIMENTO S/A")]
            BB = 820	,
            [Display(Description = "ADVALOR DTVM LTDA")]
            Advalor = 979	,
            [Display(Description = "RB CAPITAL INVESTIMENTOS DTVM")]
            RB_Capital = 1089,
            [Display(Description = "INTER DTVM LTDA")]
            Inter = 1099,
            [Display(Description = "Ourinvest DTVM S.A.")]
            Ourinvest = 1106,
            [Display(Description = "BANCO CITIBANK")]
            Citibank = 1116,
            [Display(Description = "INTL FCStone DTVM Ltda.")]
            Intl = 1130,
            [Display(Description = "CAIXA ECONOMICA FEDERAL")]
            Caixa = 1570,
            [Display(Description = "IDEAL CTVM SA")]
            Ideal = 1618,
            [Display(Description = "MODAL DTVM LTDA")]
            Modal = 1982,
            [Display(Description = "BCO FIBRA")]
            BCO_Fibra = 2197,
            [Display(Description = "ORLA DTVM S/A")]
            Orla = 2379,
            [Display(Description = "POSITIVA CTVM S/A")]
            Positiva = 2492,
            [Display(Description = "SANTANDER SECURITIES SERVICES")]
            Santander_Securities = 2570,
            [Display(Description = "LLA DTVM LTDA")]
            LLA = 2640,
            [Display(Description = "BANESTES DTVM S/A")]
            Banestes = 3112,
            [Display(Description = "RIO BRAVO INVEST S.A. DTVM")]
            Rio_Bravo = 3371,
            [Display(Description = "ORAMA DTVM S.A.")]
            Orama = 3701,
            [Display(Description = "RJI CTVM LTDA")]
            RJI = 3762,
            [Display(Description = "BANCO ANDBANK (BRASIL) S.A.")]
            AndBank = 4002,
            [Display(Description = "BS2 DTVM S/A")]
            BS2 = 4015,
            [Display(Description = "TORO CTVM LTDA.")]
            Toro = 4090,
            [Display(Description = "C6 CTVM LTDA")]
            C6 = 6003
        }

        public enum ActionType
        {
            [Display(Description = "Compra")]
            [Description("Compra")]
            Buy,
            [Display(Description = "Venda")]
            [Description("Venda")]
            Sale,
            [Display(Description = "Leilão")]
            [Description("Leilão")]
            Auction,
            [Display(Description = "Direto")]
            [Description("Direto")]
            Cross,
            [Display(Description = "RLP")]
            [Description("RLP")]
            RLP,
        }

        //public static bool CheckAnomaly(IDictionary<Agents, int> balanceByAgents, double multiplierSame, double multiplierOther, ActionType starter)
        //{
        //    try
        //    {
        //        if (balanceByAgents == null || balanceByAgents.Count == 0)
        //            return false;

        //        var maxs = balanceByAgents.OrderByDescending(q => q.Value).Take(2).ToList();
        //        var mins = balanceByAgents.OrderBy(q => q.Value).Take(2).ToList();

        //        if (starter == ActionType.Buy)
        //        {
        //            if (maxs[0].Value * multiplierSame > maxs[1].Value &&
        //                maxs[0].Value * multiplierOther > Math.Abs(mins[0].Value))
        //                return true;
        //        }
        //        else if (starter == ActionType.Sale)
        //        {
        //            if (mins[0].Value * multiplierSame < mins[1].Value &&
        //                Math.Abs(mins[0].Value * multiplierOther) > maxs[0].Value)
        //                return true;
        //        }
        //    }catch(Exception expt)
        //    {
        //        Console.WriteLine(expt.Message);
        //        Console.WriteLine("CheckAnomaly");
        //    }


        //    return false;
        //}
    }
}

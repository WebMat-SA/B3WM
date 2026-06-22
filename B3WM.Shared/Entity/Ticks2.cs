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
        public string Symbol { get; set; }

        public override string ToString()
        {
            return $"{TrydID}\t {Time.ToString("HH:mm:ss")}\t\t {Value}\t {Volume}\t {Buyer}\t\t\t {Seller}\t\t\t {Starter}";
        }

        public enum Agents
        {
            [Description("MAGLIANO S.A. CCVM")]
            [Display(Description = "MAGLIANO S.A. CCVM")]
            Magliano = 1,
            [Description("XP INVESTIMENTOS CCTVM S/A")]
            [Display(Description = "XP INVESTIMENTOS CCTVM S/A")]
            XP = 3,
            [Description("ALFA CCVM S.A.")]
            [Display(Description = "ALFA CCVM S.A.")]
            Alfa = 4,
            [Description("UBS BRASIL CCTVM S/A")]
            [Display(Description = "UBS BRASIL CCTVM S/A")]
            UBS = 8,
            [Description("MERRILL LYNCH S/A CTVM")]
            [Display(Description = "MERRILL LYNCH S/A CTVM")]
            Merrill = 13,
            [Description("GUIDE INVESTIMENTOS S.A. CV")]
            [Display(Description = "GUIDE INVESTIMENTOS S.A. CV")]
            Guide = 15,
            [Description("J. P. MORGAN CCVM S.A.")]
            [Display(Description = "J. P. MORGAN CCVM S.A.")]
            JP = 16,
            [Description("BOCOM BBM CCVM S/A")]
            [Display(Description = "BOCOM BBM CCVM S/A")]
            BOCOM = 18,
            [Description("VOTORANTIM ASSET MANAG. DTVM")]
            [Display(Description = "VOTORANTIM ASSET MANAG. DTVM")]
            Votorantim = 21,
            [Description("NECTON INVESTIMENTOS S.A. CVMC")]
            [Display(Description = "NECTON INVESTIMENTOS S.A. CVMC")]
            Necton = 23,

            [Description("SANTANDER CCVM S/A")]
            [Display(Description = "SANTANDER CCVM S/A")]
            Santander = 27,

            [Description("UNILETRA CCTVM S.A.")]
            [Display(Description = "UNILETRA CCTVM S.A.")]
            Uniletra = 29,

            [Description("LEROSA S.A. CVC")]
            [Display(Description = "LEROSA S.A. CVC")]
            Lerosa = 33,

            [Description("UM INVESTIMENTOS S.A. CTVM")]
            [Display(Description = "UM INVESTIMENTOS S.A. CTVM")]
            Um = 37,

            [Description("AGORA CTVM S/A")]
            [Display(Description = "AGORA CTVM S/A")]
            Agora = 39,

            [Description("MORGAN STANLEY CTVM S/A")]
            [Display(Description = "MORGAN STANLEY CTVM S/A")]
            Morgan = 40,

            [Description("ING CCT S/A")]
            [Display(Description = "ING CCT S/A")]
            Ing = 41,

            [Description("CREDIT SUISSE BRASIL S.A. CTVM")]
            [Display(Description = "CREDIT SUISSE BRASIL S.A. CTVM")]
            Credit = 45,

            [Description("SOCOPA SC PAULISTA S.A.")]
            [Display(Description = "SOCOPA SC PAULISTA S.A.")]
            Socopa = 58,

            [Description("SAFRA CVC LTDA.")]
            [Display(Description = "SAFRA CVC LTDA.")]
            Safra = 59,

            [Description("NOVINVEST CVM LTDA.")]
            [Display(Description = "NOVINVEST CVM LTDA.")]
            Novinvest = 63,

            [Description("BRADESCO S/A CTVM")]
            [Display(Description = "BRADESCO S/A CTVM")]
            Bradesco = 72,

            [Description("COINVALORES CCVM LTDA.")]
            [Display(Description = "COINVALORES CCVM LTDA.")]
            Coinvalores = 74,

            [Description("CITIGROUP GMB CCTVM S.A.")]
            [Display(Description = "CITIGROUP GMB CCTVM S.A.")]
            Citigroup = 77,

            [Description("MAXIMA S/A CTVM")]
            [Display(Description = "MAXIMA S/A CTVM")]
            Maxima = 83,

            [Description("BTG PACTUAL CTVM S.A.")]
            [Display(Description = "BTG PACTUAL CTVM S.A.")]
            BTG = 85,

            [Description("CM CAPITAL MARKETS CCTVM LTDA")]
            [Display(Description = "CM CAPITAL MARKETS CCTVM LTDA")]
            Capital = 88,

            [Description("NUINVEST – TITULO CV S.A.")]
            [Display(Description = "NUINVEST – TITULO CV S.A.")]
            NuInvest = 90,

            [Description("RENASCENCA DTVM LTDA.")]
            [Display(Description = "RENASCENCA DTVM LTDA.")]
            Renascenca = 92,

            [Description("NOVA FUTURA CTVM LTDA")]
            [Display(Description = "NOVA FUTURA CTVM LTDA")]
            Nova_Futura = 93,

            [Description("MERC. DO BRASIL COR. S.A. CTVM")]
            [Display(Description = "MERC. DO BRASIL COR. S.A. CTVM")]
            Mercantil = 106	,

            [Description("TERRA INVESTIMENTOS DTVM LTDA")]
            [Display(Description = "TERRA INVESTIMENTOS DTVM LTDA")]
            Terra = 107	,

            [Description("SLW CVC LTDA.")]
            [Display(Description = "SLW CVC LTDA.")]
            SLW = 110	,

            [Description("ITAU CV S/A")]
            [Display(Description = "ITAU CV S/A")]
            Itau = 114	,

            [Description("H.COMMCOR DTVM LTDA")]
            [Display(Description = "H.COMMCOR DTVM LTDA")]
            HCOMMCOR = 115,

            [Description("GENIAL INSTITUCIONAL CCTVM S.A")]
            [Display(Description = "GENIAL INSTITUCIONAL CCTVM S.A")]
            Genial = 120,

            [Description("BGC LIQUIDEZ DTVM")]
            [Display(Description = "BGC LIQUIDEZ DTVM")]
            BGC_Liquidez = 122,

            [Description("TULLETT PREBON")]
            [Display(Description = "TULLETT PREBON")]
            Tullet = 127,

            [Description("PLANNER CV S.A")]
            [Display(Description = "PLANNER CV S.A")]
            Planner = 129,

            [Description("FATOR S.A. CV")]
            [Display(Description = "FATOR S.A. CV")]
            Fator = 131,

            [Description("DIBRAN DTVM LTDA")]
            [Display(Description = "DIBRAN DTVM LTDA")]
            Dibran = 133,

            [Description("ATIVA INVESTIMENTOS S.A. CTCV")]
            [Display(Description = "ATIVA INVESTIMENTOS S.A. CTCV")]
            Ativa = 147,

            [Description("BANRISUL S/A CVMC")]
            [Display(Description = "BANRISUL S/A CVMC")]
            Banrisul = 172,

            [Description("GENIAL INVESTIMENTOS CVM S.A.")]
            [Display(Description = "GENIAL INVESTIMENTOS CVM S.A.")]
            Genial_Invest = 173,

            [Description("ELITE CCVM LTDA.")]
            [Display(Description = "ELITE CCVM LTDA.")]
            Elite = 174,

            [Description("SOLIDUS S/A CCVM")]
            [Display(Description = "SOLIDUS S/A CCVM")]
            Solidus = 177,

            [Description("MUNDINVEST S.A. CCVM")]
            [Display(Description = "MUNDINVEST S.A. CCVM")]
            Mundinvest = 181,

            [Description("CORRETORA GERAL DE VC LTDA")]
            [Display(Description = "CORRETORA GERAL DE VC LTDA")]
            Geral = 186,

            [Description("SITA SCCVM S.A.")]
            [Display(Description = "SITA SCCVM S.A.")]
            Sita = 187,

            [Description("ELLIOT WARREN.")]
            [Display(Description = "ELLIOT WARREN.")]
            Elliot_Warren = 190,

            [Description("SENSO CCVM S.A.")]
            [Display(Description = "SENSO CCVM S.A.")]
            Senso = 191,

            [Description("AMARIL FRANKLIN CTV LTDA.")]
            [Display(Description = "AMARIL FRANKLIN CTV LTDA.")]
            Amaril = 226,

            [Description("CODEPE CV E CAMBIO S/A")]
            [Display(Description = "CODEPE CV E CAMBIO S/A")]
            Codepe = 234,

            [Description("GOLDMAN SACHS DO BRASIL CTVM")]
            [Display(Description = "GOLDMAN SACHS DO BRASIL CTVM")]
            Goldman = 238,

            [Description("BANCO BNP PARIBAS BRASIL S/A")]
            [Display(Description = "BANCO BNP PARIBAS BRASIL S/A")]
            Banco_BNP = 251,

            [Description("MIRAE ASSET WEALTH MANAGEMENT")]
            [Display(Description = "MIRAE ASSET WEALTH MANAGEMENT")]
            Mirae = 262,

            [Description("CLEAR CORRETORA – Grupo XP")]
            [Display(Description = "CLEAR CORRETORA – Grupo XP")]
            Clear = 308,

            [Description("PAGINVEST")]
            [Display(Description = "PAGINVEST")]
            PAGINVEST = 357,

            [Description("BANCO DAYCOVAL")]
            [Display(Description = "BANCO DAYCOVAL")]
            Daycoval = 359,

            [Description("RICO INVESTIMENTOS – Grupo XP")]
            [Display(Description = "RICO INVESTIMENTOS – Grupo XP")]
            Rico = 386,
            [Description("BANCO OURINVEST")]
            [Display(Description = "BANCO OURINVEST")]
            Banco_Ourinvest = 442,
            [Description("BANCO MODAL")]
            [Display(Description = "BANCO MODAL")]
            Banco_Modal = 683,

            [Description("ABN AMRO CLEARING CTVM LTDA")]
            [Display(Description = "ABN AMRO CLEARING CTVM LTDA")]
            ABN_AMRO = 688,

            [Description("DILLON S.A. DTVM")]
            [Display(Description = "DILLON S.A. DTVM")]
            Dillon = 711,

            [Description("BB GESTAO DE RECURSOS DTVM S/A")]
            [Display(Description = "BB GESTAO DE RECURSOS DTVM S/A")]
            BB_Recursos = 713,

            [Description("ICAP DO BRASIL CTVM LTDA")]
            [Display(Description = "ICAP DO BRASIL CTVM LTDA")]
            Icap = 735,

            [Description("LEV DTVM LTDA")]
            [Display(Description = "LEV DTVM LTDA")]
            LEV_DTVM = 746,

            [Description("BB BANCO DE INVESTIMENTO S/A")]
            [Display(Description = "BB BANCO DE INVESTIMENTO S/A")]
            BB = 820,

            [Description("ADVALOR DTVM LTDA")]
            [Display(Description = "ADVALOR DTVM LTDA")]
            Advalor = 979,

            [Description("RB CAPITAL INVESTIMENTOS DTVM")]
            [Display(Description = "RB CAPITAL INVESTIMENTOS DTVM")]
            RB_Capital = 1089,

            [Description("INTER DTVM LTDA")]
            [Display(Description = "INTER DTVM LTDA")]
            Inter = 1099,

            [Description("Ourinvest DTVM S.A.")]
            [Display(Description = "Ourinvest DTVM S.A.")]
            Ourinvest = 1106,

            [Description("BANCO CITIBANK")]
            [Display(Description = "BANCO CITIBANK")]
            Citibank = 1116,

            [Description("INTL FCStone DTVM Ltda.")]
            [Display(Description = "INTL FCStone DTVM Ltda.")]
            Intl = 1130,

            [Description("CAIXA ECONOMICA FEDERAL")]
            [Display(Description = "CAIXA ECONOMICA FEDERAL")]
            Caixa = 1570,

            [Description("IDEAL CTVM SA")]
            [Display(Description = "IDEAL CTVM SA")]
            Ideal = 1618,

            [Description("MODAL DTVM LTDA")]
            [Display(Description = "MODAL DTVM LTDA")]
            Modal = 1982,

            [Description("BCO FIBRA")]
            [Display(Description = "BCO FIBRA")]
            BCO_Fibra = 2197,

            [Description("ORLA DTVM S/A")]
            [Display(Description = "ORLA DTVM S/A")]
            Orla = 2379,

            [Description("POSITIVA CTVM S/A")]
            [Display(Description = "POSITIVA CTVM S/A")]
            Positiva = 2492,

            [Description("SANTANDER SECURITIES SERVICES")]
            [Display(Description = "SANTANDER SECURITIES SERVICES")]
            Santander_Securities = 2570,
            [Description("LLA DTVM LTDA")]
            [Display(Description = "LLA DTVM LTDA")]
            LLA = 2640,
            [Description("BANESTES DTVM S/A")]
            [Display(Description = "BANESTES DTVM S/A")]
            Banestes = 3112,

            [Description("RIO BRAVO INVEST S.A. DTVM")]
            [Display(Description = "RIO BRAVO INVEST S.A. DTVM")]
            Rio_Bravo = 3371,

            [Description("ORAMA DTVM S.A.")]
            [Display(Description = "ORAMA DTVM S.A.")]
            Orama = 3701,

            [Description("RJI CTVM LTDA")]
            [Display(Description = "RJI CTVM LTDA")]
            RJI = 3762,

            [Description("BANCO ANDBANK (BRASIL) S.A.")]
            [Display(Description = "BANCO ANDBANK (BRASIL) S.A.")]
            AndBank = 4002,

            [Description("BS2 DTVM S/A")]
            [Display(Description = "BS2 DTVM S/A")]
            BS2 = 4015,

            [Description("TORO CTVM LTDA.")]
            [Display(Description = "TORO CTVM LTDA.")]
            Toro = 4090,

            [Description("C6 CTVM LTDA")]
            [Display(Description = "C6 CTVM LTDA")]
            C6 = 6003
        }
        public enum ActionType
        {
            [Display(Description = "Compra")]
            [Description("Compra")]
            Buy = 1,
            [Display(Description = "Venda")]
            [Description("Venda")]
            Sale = 2,
            [Display(Description = "Leilão")]
            [Description("Leilão")]
            Auction = 3,
            [Display(Description = "Direto")]
            [Description("Direto")]
            Cross = 4,
            [Display(Description = "RLP")]
            [Description("RLP")]
            RLP = 5,
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

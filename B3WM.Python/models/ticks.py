from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from enum import Enum
from typing import Optional


class DescribedEnum(Enum):
    def __new__(cls, value: int, description: str):
        obj = object.__new__(cls)
        obj._value_ = value
        obj.description = description
        return obj

    def __str__(self) -> str:
        return self.name

    @property
    def display(self) -> str:
        return self.description


class Agents(DescribedEnum):
    Magliano = (1, "MAGLIANO S.A. CCVM")
    XP = (3, "XP INVESTIMENTOS CCTVM S/A")
    Alfa = (4, "ALFA CCVM S.A.")
    UBS = (8, "UBS BRASIL CCTVM S/A")
    Merrill = (13, "MERRILL LYNCH S/A CTVM")
    Guide = (15, "GUIDE INVESTIMENTOS S.A. CV")
    JP = (16, "J. P. MORGAN CCVM S.A.")
    BOCOM = (18, "BOCOM BBM CCVM S/A")
    Votorantim = (21, "VOTORANTIM ASSET MANAG. DTVM")
    Necton = (23, "NECTON INVESTIMENTOS S.A. CVMC")
    Santander = (27, "SANTANDER CCVM S/A")
    Uniletra = (29, "UNILETRA CCTVM S.A.")
    Lerosa = (33, "LEROSA S.A. CVC")
    Um = (37, "UM INVESTIMENTOS S.A. CTVM")
    Agora = (39, "AGORA CTVM S/A")
    Morgan = (40, "MORGAN STANLEY CTVM S/A")
    Ing = (41, "ING CCT S/A")
    Credit = (45, "CREDIT SUISSE BRASIL S.A. CTVM")
    Socopa = (58, "SOCOPA SC PAULISTA S.A.")
    Safra = (59, "SAFRA CVC LTDA.")
    Novinvest = (63, "NOVINVEST CVM LTDA.")
    Bradesco = (72, "BRADESCO S/A CTVM")
    Coinvalores = (74, "COINVALORES CCVM LTDA.")
    Citigroup = (77, "CITIGROUP GMB CCTVM S.A.")
    Maxima = (83, "MAXIMA S/A CTVM")
    BTG = (85, "BTG PACTUAL CTVM S.A.")
    Capital = (88, "CM CAPITAL MARKETS CCTVM LTDA")
    NuInvest = (90, "NUINVEST – TITULO CV S.A.")
    Renascenca = (92, "RENASCENCA DTVM LTDA.")
    Nova_Futura = (93, "NOVA FUTURA CTVM LTDA")
    Mercantil = (106, "MERC. DO BRASIL COR. S.A. CTVM")
    Terra = (107, "TERRA INVESTIMENTOS DTVM LTDA")
    SLW = (110, "SLW CVC LTDA.")
    Itau = (114, "ITAU CV S/A")
    HCOMMCOR = (115, "H.COMMCOR DTVM LTDA")
    Genial = (120, "GENIAL INSTITUCIONAL CCTVM S.A")
    BGC_Liquidez = (122, "BGC LIQUIDEZ DTVM")
    Tullet = (127, "TULLETT PREBON")
    Planner = (129, "PLANNER CV S.A")
    Fator = (131, "FATOR S.A. CV")
    Dibran = (133, "DIBRAN DTVM LTDA")
    Ativa = (147, "ATIVA INVESTIMENTOS S.A. CTCV")
    Banrisul = (172, "BANRISUL S/A CVMC")
    Genial_Invest = (173, "GENIAL INVESTIMENTOS CVM S.A.")
    Elite = (174, "ELITE CCVM LTDA.")
    Solidus = (177, "SOLIDUS S/A CCVM")
    Mundinvest = (181, "MUNDINVEST S.A. CCVM")
    Geral = (186, "CORRETORA GERAL DE VC LTDA")
    Sita = (187, "SITA SCCVM S.A.")
    Senso = (191, "SENSO CCVM S.A.")
    Amaril = (226, "AMARIL FRANKLIN CTV LTDA.")
    Codepe = (234, "CODEPE CV E CAMBIO S/A")
    Goldman = (238, "GOLDMAN SACHS DO BRASIL CTVM")
    Banco_BNP = (251, "BANCO BNP PARIBAS BRASIL S/A")
    Mirae = (262, "MIRAE ASSET WEALTH MANAGEMENT")
    Clear = (308, "CLEAR CORRETORA – Grupo XP")
    Daycoval = (359, "BANCO DAYCOVAL")
    Rico = (386, "RICO INVESTIMENTOS – Grupo XP")
    Banco_Ourinvest = (442, "BANCO OURINVEST")
    Banco_Modal = (683, "BANCO MODAL")
    ABN_AMRO = (688, "ABN AMRO CLEARING CTVM LTDA")
    Dillon = (711, "DILLON S.A. DTVM")
    BB_Recursos = (713, "BB GESTAO DE RECURSOS DTVM S/A")
    Icap = (735, "ICAP DO BRASIL CTVM LTDA")
    LEV_DTVM = (746, "LEV DTVM LTDA")
    BB = (820, "BB BANCO DE INVESTIMENTO S/A")
    Advalor = (979, "ADVALOR DTVM LTDA")
    RB_Capital = (1089, "RB CAPITAL INVESTIMENTOS DTVM")
    Inter = (1099, "INTER DTVM LTDA")
    Ourinvest = (1106, "Ourinvest DTVM S.A.")
    Citibank = (1116, "BANCO CITIBANK")
    Intl = (1130, "INTL FCStone DTVM Ltda.")
    Caixa = (1570, "CAIXA ECONOMICA FEDERAL")
    Ideal = (1618, "IDEAL CTVM SA")
    Modal = (1982, "MODAL DTVM LTDA")
    BCO_Fibra = (2197, "BCO FIBRA")
    Orla = (2379, "ORLA DTVM S/A")
    Positiva = (2492, "POSITIVA CTVM S/A")
    Santander_Securities = (2570, "SANTANDER SECURITIES SERVICES")
    LLA = (2640, "LLA DTVM LTDA")
    Banestes = (3112, "BANESTES DTVM S/A")
    Rio_Bravo = (3371, "RIO BRAVO INVEST S.A. DTVM")
    Orama = (3701, "ORAMA DTVM S.A.")
    RJI = (3762, "RJI CTVM LTDA")
    AndBank = (4002, "BANCO ANDBANK (BRASIL) S.A.")
    BS2 = (4015, "BS2 DTVM S/A")
    Toro = (4090, "TORO CTVM LTDA.")
    C6 = (6003, "C6 CTVM LTDA")


class ActionType(DescribedEnum):
    Buy = (1, "Compra")
    Sale = (2, "Venda")
    Auction = (3, "Leilão")
    Cross = (4, "Direto")
    RLP = (5, "RLP")


@dataclass
class Ticks2:
    Time: datetime = datetime.min
    Value: float = 0.0
    Volume: int = 0
    Buyer: Optional[Agents] = None
    Seller: Optional[Agents] = None
    Starter: Optional[ActionType] = None
    Symbol: str = ""

    def __str__(self) -> str:
        buyer = self.Buyer.name if self.Buyer else ""
        seller = self.Seller.name if self.Seller else ""
        starter = self.Starter.name if self.Starter else ""
        return (
            f"{self.Time.strftime('%H:%M:%S')}\t\t "
            f"{self.Value}\t {self.Volume}\t {buyer}\t\t\t {seller}\t\t\t {starter}"
        )

import 'package:flutter/foundation.dart';

enum ActionType {
  buy(1, 'Compra'),
  sale(2, 'Venda'),
  auction(3, 'Leilão'),
  cross(4, 'Direto'),
  rlp(5, 'RLP');

  final int value;
  final String description;
  const ActionType(this.value, this.description);

  static ActionType fromValue(int value) {
    return ActionType.values.firstWhere(
      (e) => e.value == value,
      orElse: () => ActionType.buy,
    );
  }
}

@immutable
class Agents {
  final int value;
  final String description;

  const Agents._(this.value, this.description);

  static const magliano = Agents._(1, 'MAGLIANO S.A. CCVM');
  static const xp = Agents._(3, 'XP INVESTIMENTOS CCTVM S/A');
  static const alfa = Agents._(4, 'ALFA CCVM S.A.');
  static const ubs = Agents._(8, 'UBS BRASIL CCTVM S/A');
  static const merrill = Agents._(13, 'MERRILL LYNCH S/A CTVM');
  static const guide = Agents._(15, 'GUIDE INVESTIMENTOS S.A. CV');
  static const jp = Agents._(16, 'J. P. MORGAN CCVM S.A.');
  static const bocom = Agents._(18, 'BOCOM BBM CCVM S/A');
  static const votorantim = Agents._(21, 'VOTORANTIM ASSET MANAG. DTVM');
  static const necton = Agents._(23, 'NECTON INVESTIMENTOS S.A. CVMC');
  static const santander = Agents._(27, 'SANTANDER CCVM S/A');
  static const uniletra = Agents._(29, 'UNILETRA CCTVM S.A.');
  static const lerosa = Agents._(33, 'LEROSA S.A. CVC');
  static const um = Agents._(37, 'UM INVESTIMENTOS S.A. CTVM');
  static const agora = Agents._(39, 'AGORA CTVM S/A');
  static const morgan = Agents._(40, 'MORGAN STANLEY CTVM S/A');
  static const ing = Agents._(41, 'ING CCT S/A');
  static const credit = Agents._(45, 'CREDIT SUISSE BRASIL S.A. CTVM');
  static const socopa = Agents._(58, 'SOCOPA SC PAULISTA S.A.');
  static const safra = Agents._(59, 'SAFRA CVC LTDA.');
  static const novinvest = Agents._(63, 'NOVINVEST CVM LTDA.');
  static const bradesco = Agents._(72, 'BRADESCO S/A CTVM');
  static const coinvalores = Agents._(74, 'COINVALORES CCVM LTDA.');
  static const citigroup = Agents._(77, 'CITIGROUP GMB CCTVM S.A.');
  static const maxima = Agents._(83, 'MAXIMA S/A CTVM');
  static const btg = Agents._(85, 'BTG PACTUAL CTVM S.A.');
  static const capital = Agents._(88, 'CM CAPITAL MARKETS CCTVM LTDA');
  static const nuInvest = Agents._(90, 'NUINVEST – TITULO CV S.A.');
  static const renascenca = Agents._(92, 'RENASCENCA DTVM LTDA.');
  static const novaFutura = Agents._(93, 'NOVA FUTURA CTVM LTDA');
  static const mercantil = Agents._(106, 'MERC. DO BRASIL COR. S.A. CTVM');
  static const terra = Agents._(107, 'TERRA INVESTIMENTOS DTVM LTDA');
  static const slw = Agents._(110, 'SLW CVC LTDA.');
  static const itau = Agents._(114, 'ITAU CV S/A');
  static const hcommcor = Agents._(115, 'H.COMMCOR DTVM LTDA');
  static const genial = Agents._(120, 'GENIAL INSTITUCIONAL CCTVM S.A');
  static const bgcLiquidez = Agents._(122, 'BGC LIQUIDEZ DTVM');
  static const tullet = Agents._(127, 'TULLETT PREBON');
  static const planner = Agents._(129, 'PLANNER CV S.A');
  static const fator = Agents._(131, 'FATOR S.A. CV');
  static const dibran = Agents._(133, 'DIBRAN DTVM LTDA');
  static const ativa = Agents._(147, 'ATIVA INVESTIMENTOS S.A. CTCV');
  static const banrisul = Agents._(172, 'BANRISUL S/A CVMC');
  static const genialInvest = Agents._(173, 'GENIAL INVESTIMENTOS CVM S.A.');
  static const elite = Agents._(174, 'ELITE CCVM LTDA.');
  static const solidus = Agents._(177, 'SOLIDUS S/A CCVM');
  static const mundinvest = Agents._(181, 'MUNDINVEST S.A. CCVM');
  static const geral = Agents._(186, 'CORRETORA GERAL DE VC LTDA');
  static const sita = Agents._(187, 'SITA SCCVM S.A.');
  static const elliotWarren = Agents._(190, 'ELLIOT WARREN.');
  static const senso = Agents._(191, 'SENSO CCVM S.A.');
  static const amaril = Agents._(226, 'AMARIL FRANKLIN CTV LTDA.');
  static const codepe = Agents._(234, 'CODEPE CV E CAMBIO S/A');
  static const goldman = Agents._(238, 'GOLDMAN SACHS DO BRASIL CTVM');
  static const bancoBnp = Agents._(251, 'BANCO BNP PARIBAS BRASIL S/A');
  static const mirae = Agents._(262, 'MIRAE ASSET WEALTH MANAGEMENT');
  static const clear = Agents._(308, 'CLEAR CORRETORA – Grupo XP');
  static const paginvest = Agents._(357, 'PAGINVEST');
  static const daycoval = Agents._(359, 'BANCO DAYCOVAL');
  static const rico = Agents._(386, 'RICO INVESTIMENTOS – Grupo XP');
  static const bancoOurinvest = Agents._(442, 'BANCO OURINVEST');
  static const bancoModal = Agents._(683, 'BANCO MODAL');
  static const abnAmro = Agents._(688, 'ABN AMRO CLEARING CTVM LTDA');
  static const dillon = Agents._(711, 'DILLON S.A. DTVM');
  static const bbRecursos = Agents._(713, 'BB GESTAO DE RECURSOS DTVM S/A');
  static const icap = Agents._(735, 'ICAP DO BRASIL CTVM LTDA');
  static const levDtvM = Agents._(746, 'LEV DTVM LTDA');
  static const bb = Agents._(820, 'BB BANCO DE INVESTIMENTO S/A');
  static const advalor = Agents._(979, 'ADVALOR DTVM LTDA');
  static const rbCapital = Agents._(1089, 'RB CAPITAL INVESTIMENTOS DTVM');
  static const inter = Agents._(1099, 'INTER DTVM LTDA');
  static const ourinvest = Agents._(1106, 'Ourinvest DTVM S.A.');
  static const citibank = Agents._(1116, 'BANCO CITIBANK');
  static const intl = Agents._(1130, 'INTL FCStone DTVM Ltda.');
  static const caixa = Agents._(1570, 'CAIXA ECONOMICA FEDERAL');
  static const ideal = Agents._(1618, 'IDEAL CTVM SA');
  static const modal = Agents._(1982, 'MODAL DTVM LTDA');
  static const bcoFibra = Agents._(2197, 'BCO FIBRA');
  static const orla = Agents._(2379, 'ORLA DTVM S/A');
  static const positiva = Agents._(2492, 'POSITIVA CTVM S/A');
  static const santanderSecurities = Agents._(2570, 'SANTANDER SECURITIES SERVICES');
  static const lla = Agents._(2640, 'LLA DTVM LTDA');
  static const banestes = Agents._(3112, 'BANESTES DTVM S/A');
  static const rioBravo = Agents._(3371, 'RIO BRAVO INVEST S.A. DTVM');
  static const orama = Agents._(3701, 'ORAMA DTVM S.A.');
  static const rji = Agents._(3762, 'RJI CTVM LTDA');
  static const andBank = Agents._(4002, 'BANCO ANDBANK (BRASIL) S.A.');
  static const bs2 = Agents._(4015, 'BS2 DTVM S/A');
  static const toro = Agents._(4090, 'TORO CTVM LTDA.');
  static const c6 = Agents._(6003, 'C6 CTVM LTDA');

  static final Map<int, Agents> _byValue = {
    for (final a in _all) a.value: a,
  };

  static final List<Agents> _all = [
    magliano, xp, alfa, ubs, merrill, guide, jp, bocom, votorantim,
    necton, santander, uniletra, lerosa, um, agora, morgan, ing, credit,
    socopa, safra, novinvest, bradesco, coinvalores, citigroup, maxima,
    btg, capital, nuInvest, renascenca, novaFutura, mercantil, terra,
    slw, itau, hcommcor, genial, bgcLiquidez, tullet, planner, fator,
    dibran, ativa, banrisul, genialInvest, elite, solidus, mundinvest,
    geral, sita, elliotWarren, senso, amaril, codepe, goldman, bancoBnp,
    mirae, clear, paginvest, daycoval, rico, bancoOurinvest, bancoModal,
    abnAmro, dillon, bbRecursos, icap, levDtvM, bb, advalor, rbCapital,
    inter, ourinvest, citibank, intl, caixa, ideal, modal, bcoFibra,
    orla, positiva, santanderSecurities, lla, banestes, rioBravo, orama,
    rji, andBank, bs2, toro, c6,
  ];

  static Agents? fromValue(int value) => _byValue[value];

  @override
  bool operator ==(Object other) =>
      identical(this, other) || (other is Agents && value == other.value);

  @override
  int get hashCode => value.hashCode;
}

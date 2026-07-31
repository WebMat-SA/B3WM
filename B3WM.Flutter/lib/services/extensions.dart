extension DateTimeExtensions on DateTime {
  DateTime getCandleStart(int timeFrameMinutes) {
    if (timeFrameMinutes <= 0) {
      throw ArgumentError('TimeFrame must be greater than zero');
    }
    final totalMinutes = hour * 60 + minute;
    final candleStartMinutes = (totalMinutes ~/ timeFrameMinutes) * timeFrameMinutes;
    return DateTime(year, month, day, candleStartMinutes ~/ 60, candleStartMinutes % 60);
  }

  Duration getRemainingTimeCandle(DateTime currentTime, int timeFrameMinutes) {
    if (timeFrameMinutes <= 0) {
      throw ArgumentError('TimeFrame must be greater than zero');
    }
    final totalMinutes = hour * 60 + minute;
    final candleStartMinutes = (totalMinutes ~/ timeFrameMinutes) * timeFrameMinutes;
    final candleEndMinutes = candleStartMinutes + timeFrameMinutes;
    final candleEnd = DateTime(year, month, day, candleEndMinutes ~/ 60, candleEndMinutes % 60);
    return candleEnd.difference(currentTime);
  }
}

extension AgentsExtension on int {
  String agentDescription() {
    // This is handled by the Agents class directly in ticks2.dart
    return agentsDescription(this);
  }
}

String agentsDescription(int agentValue) {
  final agents = {
    1: 'MAGLIANO S.A. CCVM',
    3: 'XP INVESTIMENTOS CCTVM S/A',
    4: 'ALFA CCVM S.A.',
    8: 'UBS BRASIL CCTVM S/A',
    13: 'MERRILL LYNCH S/A CTVM',
    15: 'GUIDE INVESTIMENTOS S.A. CV',
    16: 'J. P. MORGAN CCVM S.A.',
    18: 'BOCOM BBM CCVM S/A',
    21: 'VOTORANTIM ASSET MANAG. DTVM',
    23: 'NECTON INVESTIMENTOS S.A. CVMC',
    27: 'SANTANDER CCVM S/A',
    29: 'UNILETRA CCTVM S.A.',
    33: 'LEROSA S.A. CVC',
    37: 'UM INVESTIMENTOS S.A. CTVM',
    39: 'AGORA CTVM S/A',
    40: 'MORGAN STANLEY CTVM S/A',
    41: 'ING CCT S/A',
    45: 'CREDIT SUISSE BRASIL S.A. CTVM',
    58: 'SOCOPA SC PAULISTA S.A.',
    59: 'SAFRA CVC LTDA.',
    63: 'NOVINVEST CVM LTDA.',
    72: 'BRADESCO S/A CTVM',
    74: 'COINVALORES CCVM LTDA.',
    77: 'CITIGROUP GMB CCTVM S.A.',
    83: 'MAXIMA S/A CTVM',
    85: 'BTG PACTUAL CTVM S.A.',
    88: 'CM CAPITAL MARKETS CCTVM LTDA',
    90: 'NUINVEST – TITULO CV S.A.',
    92: 'RENASCENCA DTVM LTDA.',
    93: 'NOVA FUTURA CTVM LTDA',
    106: 'MERC. DO BRASIL COR. S.A. CTVM',
    107: 'TERRA INVESTIMENTOS DTVM LTDA',
    110: 'SLW CVC LTDA.',
    114: 'ITAU CV S/A',
    115: 'H.COMMCOR DTVM LTDA',
    120: 'GENIAL INSTITUCIONAL CCTVM S.A',
    122: 'BGC LIQUIDEZ DTVM',
    127: 'TULLETT PREBON',
    129: 'PLANNER CV S.A',
    131: 'FATOR S.A. CV',
    133: 'DIBRAN DTVM LTDA',
    147: 'ATIVA INVESTIMENTOS S.A. CTCV',
    172: 'BANRISUL S/A CVMC',
    173: 'GENIAL INVESTIMENTOS CVM S.A.',
    174: 'ELITE CCVM LTDA.',
    177: 'SOLIDUS S/A CCVM',
    181: 'MUNDINVEST S.A. CCVM',
    186: 'CORRETORA GERAL DE VC LTDA',
    187: 'SITA SCCVM S.A.',
    190: 'ELLIOT WARREN.',
    191: 'SENSO CCVM S.A.',
    226: 'AMARIL FRANKLIN CTV LTDA.',
    234: 'CODEPE CV E CAMBIO S/A',
    238: 'GOLDMAN SACHS DO BRASIL CTVM',
    251: 'BANCO BNP PARIBAS BRASIL S/A',
    262: 'MIRAE ASSET WEALTH MANAGEMENT',
    308: 'CLEAR CORRETORA – Grupo XP',
    357: 'PAGINVEST',
    359: 'BANCO DAYCOVAL',
    386: 'RICO INVESTIMENTOS – Grupo XP',
    442: 'BANCO OURINVEST',
    683: 'BANCO MODAL',
    688: 'ABN AMRO CLEARING CTVM LTDA',
    711: 'DILLON S.A. DTVM',
    713: 'BB GESTAO DE RECURSOS DTVM S/A',
    735: 'ICAP DO BRASIL CTVM LTDA',
    746: 'LEV DTVM LTDA',
    820: 'BB BANCO DE INVESTIMENTO S/A',
    979: 'ADVALOR DTVM LTDA',
    1089: 'RB CAPITAL INVESTIMENTOS DTVM',
    1099: 'INTER DTVM LTDA',
    1106: 'Ourinvest DTVM S.A.',
    1116: 'BANCO CITIBANK',
    1130: 'INTL FCStone DTVM Ltda.',
    1570: 'CAIXA ECONOMICA FEDERAL',
    1618: 'IDEAL CTVM SA',
    1982: 'MODAL DTVM LTDA',
    2197: 'BCO FIBRA',
    2379: 'ORLA DTVM S/A',
    2492: 'POSITIVA CTVM S/A',
    2570: 'SANTANDER SECURITIES SERVICES',
    2640: 'LLA DTVM LTDA',
    3112: 'BANESTES DTVM S/A',
    3371: 'RIO BRAVO INVEST S.A. DTVM',
    3701: 'ORAMA DTVM S.A.',
    3762: 'RJI CTVM LTDA',
    4002: 'BANCO ANDBANK (BRASIL) S.A.',
    4015: 'BS2 DTVM S/A',
    4090: 'TORO CTVM LTDA.',
    6003: 'C6 CTVM LTDA',
  };
  return agents[agentValue] ?? 'Desconhecido';
}

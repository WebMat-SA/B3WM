import '../../../models/structure_change_item.dart';
import '../../../services/state_service.dart';

String fmtDailyDate(DateTime d) =>
    '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';

/// Janela do widget diário: preset por dias ou âncora automática.
/// [windowDays] 0 = âncora (última perna do 1440), senão N dias.
String dailyWindowLabel(int windowDays) => windowDays <= 0
    ? 'Âncora (última perna 1440)'
    : 'Últimos $windowDays dias';

/// Cadeia de rastreabilidade do widget diário: qual quebra do 1440 ancorou
/// a janela, qual janela e qual perfil gerou linhas e barras.
String dailyWindowText(StateService state) {
  final from = state.dailyExtremeFrom ?? state.dailyProfileFrom;
  final to = state.dailyExtremeTo ?? state.dailyProfileTo;
  if (from == null || to == null) {
    return 'Janela 1D: ${dailyWindowLabel(state.dailyWindowDays)}';
  }
  final buf = StringBuffer('Janela 1D: ${fmtDailyDate(from)} → ${fmtDailyDate(to)}');
  final StructureChangeItem? anchor = state.dailyAnchor;
  if (anchor != null) {
    final dir = anchor.isUp ? 'UP' : 'BT';
    buf.write(
        '\nÂncora 1D: $dir ${anchor.oldValue.toStringAsFixed(2)} → ${anchor.newValue.toStringAsFixed(2)} em ${fmtDailyDate(anchor.date)}');
  } else if (state.dailyWindowDays <= 0) {
    buf.write('\nSem quebra 1440 — últimos 60 dias');
  }
  final levels = state.dailyProfileLevels.length;
  if (levels > 0) buf.write(' • $levels níveis no perfil');
  final dex = state.dailyExtremes;
  if (dex != null && dex.extremes.isNotEmpty) {
    buf.write(' • ${dex.topCount} topo(s) / ${dex.valleyCount} vale(s)');
  }
  return buf.toString();
}

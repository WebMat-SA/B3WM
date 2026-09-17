import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/state_service.dart';
import '../../models/defaults.dart';
import '../../models/extreme_storage_item.dart';
import '../../models/structure_change_item.dart';
import 'drawer_controls.dart';

class ExtremeDrawer extends StatefulWidget {
  final bool noDrawer;
  const ExtremeDrawer({super.key, this.noDrawer = false});

  @override
  State<ExtremeDrawer> createState() => _ExtremeDrawerState();
}

class _ExtremeDrawerState extends State<ExtremeDrawer>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  bool _isDailyStructureLoading = false;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Consumer<StateService>(builder: (context, state, _) {
      final ex = state.extremes;
      final dex = state.dailyExtremes;
      final body = ListView(
        padding: EdgeInsets.zero,
        children: [
          ExpandableSection(
            icon: Icons.terrain,
            title: 'Topos/Vales',
            defaultExpanded: true,
            child: Column(
              children: [
                ToggleRow('Show on Chart', state.extremeVisible,
                    (v) => state.setExtremeVisible(v)),
                SliderRow('Opacity', state.extremeOpacity, 0, 1,
                    (v) => state.setExtremeOpacity(v)),
                SliderRow(
                  'Noise Sensitivity',
                  state.extremeNoiseSensitivity,
                  Defaults.extremeNoiseSensitivityMin,
                  Defaults.extremeNoiseSensitivityMax,
                  (v) => state.setExtremeNoiseSensitivity(v),
                  decimals: 1,
                  step: Defaults.extremeNoiseSensitivityStep,
                ),
                SliderRow(
                  'Min Prominence',
                  state.extremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                ),
              ],
            ),
          ),
          ExpandableSection(
            icon: Icons.calendar_month,
            title: 'Referências Diárias (1D — estático)',
            defaultExpanded: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ToggleRow('Show daily on Chart', state.dailyExtremeVisible,
                    (v) => state.setDailyExtremeVisible(v)),
                ToggleRow('Show daily structure (1D)', state.dailyStructureVisible,
                    (v) => state.setDailyStructureVisible(v)),
                SliderRow('Opacity (D)', state.dailyExtremeOpacity, 0, 1,
                    (v) => state.setDailyExtremeOpacity(v)),
                SliderRow(
                  'Noise (D)',
                  state.dailyExtremeNoiseSensitivity,
                  Defaults.extremeNoiseSensitivityMin,
                  Defaults.extremeNoiseSensitivityMax,
                  (v) => state.setDailyExtremeNoiseSensitivity(v),
                  decimals: 1,
                  step: Defaults.extremeNoiseSensitivityStep,
                ),
                SliderRow(
                  'Prominence (D)',
                  state.dailyExtremeMinimumProminence,
                  Defaults.extremeMinimumProminenceMin,
                  Defaults.extremeMinimumProminenceMax,
                  (v) => state.setDailyExtremeMinimumProminence(v),
                  decimals: 2,
                  step: Defaults.extremeMinimumProminenceStep,
                ),
                SliderRow(
                  'Range 1D (estruturas)',
                  state.structureRangeUpdDaily,
                  0,
                  Defaults.structureRangeUpdDailyMax(state.symbol),
                  (v) => state.setStructureRangeUpdDaily(v),
                  decimals: 0,
                  step: Defaults.structureRangeUpdDailyStep(state.symbol),
                  trailing: state.isDailyStructureUpdating
                      ? _isDailyStructureLoading
                          ? const Padding(
                              padding: EdgeInsets.only(left: 4),
                              child: SizedBox(
                                  width: 20,
                                  height: 20,
                                  child:
                                      CircularProgressIndicator(strokeWidth: 2)))
                          : IconButton(
                              icon: const Icon(Icons.upload,
                                  color: Colors.orange),
                              tooltip: 'Aplicar Range 1D (só 1440)',
                              onPressed: () async {
                                setState(() =>
                                    _isDailyStructureLoading = true);
                                await state.confirmStructureRangeUpdDaily();
                                if (mounted) {
                                  setState(() =>
                                      _isDailyStructureLoading = false);
                                }
                              },
                              constraints: const BoxConstraints(),
                              padding: EdgeInsets.zero,
                              visualDensity: VisualDensity.compact,
                            )
                      : null,
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Text(
                    _dailyWindowText(state, dex),
                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          icon: state.isDailyExtremeLoading
                              ? const SizedBox(
                                  width: 14,
                                  height: 14,
                                  child: CircularProgressIndicator(strokeWidth: 2))
                              : const Icon(Icons.refresh, size: 16),
                          label: Text(state.isDailyExtremeLoading
                              ? 'Atualizando...'
                              : 'Atualizar diário'),
                          onPressed: state.isDailyExtremeLoading
                              ? null
                              : () => state.loadDailyExtremes(),
                        ),
                      ),
                      if (dex != null) ...[
                        const SizedBox(width: 8),
                        IconButton(
                          tooltip: 'Limpar',
                          icon: const Icon(Icons.clear, size: 18),
                          onPressed: () => state.clearDailyExtremes(),
                        ),
                      ],
                    ],
                  ),
                ),
                if (dex != null && dex.extremes.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    child: Text(
                      'Diário: ${dex.topCount} topo(s) / ${dex.valleyCount} vale(s)'
                      '${dex.profilePointCount > 0 ? ' • ${dex.profilePointCount} níveis no perfil' : ''}',
                      style: const TextStyle(
                          fontSize: 12, fontWeight: FontWeight.bold),
                    ),
                  ),
                if (dex != null)
                  for (final e in dex.extremes) _ExtremeRow(point: e, isDaily: true),
                if (dex == null)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    child: Text(
                      state.isDailyExtremeLoading
                          ? 'Carregando níveis diários...'
                          : 'Carrega sozinho ao abrir o símbolo. Toque em Atualizar após mudar Noise/Prominence (D).',
                      style: const TextStyle(fontSize: 11, color: Colors.grey),
                    ),
                  ),
              ],
            ),
          ),
          if (ex != null && ex.extremes.isNotEmpty)
            ExpandableSection(
              icon: Icons.list_alt,
              title:
                  'Detectados (${ex.topCount} topo(s) / ${ex.valleyCount} vale(s))',
              defaultExpanded: true,
              child: Column(
                children: [
                  for (final e in ex.extremes)
                    _ExtremeRow(point: e),
                ],
              ),
            ),
          if (ex == null)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('Nenhuma detecção carregada.',
                  style: TextStyle(fontSize: 13, color: Colors.grey)),
            ),
        ],
      );
      if (widget.noDrawer) return body;
      return Drawer(width: 360, child: body);
    });
  }
}

class _ExtremeRow extends StatelessWidget {
  final ExtremePoint point;
  final bool isDaily;
  const _ExtremeRow({required this.point, this.isDaily = false});

  @override
  Widget build(BuildContext context) {
    final isTop = point.type == ExtremeType.top;
    final color = isDaily
        ? (isTop ? const Color(0xFF43A047) : const Color(0xFFE53935))
        : isTop
            ? const Color(0xFF69F0AE)
            : point.type == ExtremeType.valley
                ? const Color(0xFFFF5252)
                : Colors.grey;
    final label = isTop
        ? 'Topo'
        : point.type == ExtremeType.valley
            ? 'Vale'
            : 'Indeterminado';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Container(
            width: 10,
            height: 10,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 84,
            child: Text(label,
                style: TextStyle(fontSize: 12, color: color)),
          ),
          Expanded(
            child: Text(point.position.toStringAsFixed(1),
                style: const TextStyle(fontSize: 12)),
          ),
          Text('imp ${point.prominence.toStringAsFixed(2)}',
              style: const TextStyle(fontSize: 11, color: Colors.grey)),
        ],
      ),
    );
  }
}

String _fmtDate(DateTime d) =>
    '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';

/// Cadeia de rastreabilidade do overlay diário: qual quebra do 1440 ancorou
/// a janela, qual janela e qual perfil gerou as linhas.
String _dailyWindowText(StateService state, ExtremeStorageItem? dex) {
  final from = state.dailyExtremeFrom;
  final to = state.dailyExtremeTo;
  if (from == null || to == null) {
    return 'Janela 1D: auto pela última perna do 1440';
  }
  final buf = StringBuffer('Janela 1D: ${_fmtDate(from)} → ${_fmtDate(to)}');
  final StructureChangeItem? anchor = state.dailyAnchor;
  if (anchor != null) {
    final dir = anchor.isUp ? 'UP' : 'BT';
    buf.write(
        '\nÂncora 1D: $dir ${anchor.oldValue.toStringAsFixed(2)} → ${anchor.newValue.toStringAsFixed(2)} em ${_fmtDate(anchor.date)}');
  } else {
    buf.write('\nSem quebra 1440 — últimos 60 dias');
  }
  final levels = dex?.profilePointCount ?? 0;
  if (levels > 0) buf.write(' • $levels níveis no perfil');
  return buf.toString();
}
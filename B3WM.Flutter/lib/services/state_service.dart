import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'package:flutter/foundation.dart';
import '../models/bar_storage_item.dart';
import '../models/config_backup.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/structure_change_item.dart';
import '../models/symbol_config.dart'
    show DateRangeMode, SymbolConfig;
import '../models/daily_analysis_config.dart';
import 'api_service.dart';
import 'strategy_service.dart';
import 'signalr_service.dart';
import 'preferences_service.dart';
import 'audio_service.dart';
import '../models/trade_models.dart';
import '../models/extreme_storage_item.dart';
import '../models/pivot_storage_item.dart';
import '../models/defaults.dart';
import '../app_log.dart';

class StateService extends ChangeNotifier {
  final ApiService _apiService;
  final SignalRService _signalRService;
  final PreferencesService _preferencesService;
  final AudioService _audioService;

  StateService({
    required ApiService apiService,
    required SignalRService signalRService,
    required PreferencesService preferencesService,
    required AudioService audioService,
    StrategyService? strategyService,
  })  : _apiService = apiService,
        _signalRService = signalRService,
        _preferencesService = preferencesService,
        _audioService = audioService,
        _strategyService = strategyService ?? StrategyService(),
        _ownsStrategyService = strategyService == null {
    _init();
  }

  // --- State ---
  String _symbol = '';
  String get symbol => _symbol;

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  int _throttlingDelayMs = 500;
  int get throttlingDelayMs => _throttlingDelayMs;

  List<BarStorageItem> _bars = [];
  List<BarStorageItem> get bars => _bars;

  List<BarStorageItem> get barsTimeFrameFilter =>
      _bars.where((b) => b.timeFrame == _currentConfig.timeFrame).toList()
        ..sort((a, b) => a.date.compareTo(b.date));

  BarStorageItem? _currentBar;
  BarStorageItem? get currentBar => _currentBar;

  List<BubbleStorageItem> _bubbles = [];
  List<BubbleStorageItem> get bubbles => _bubbles;

  List<VolumeLevel> _volumeLevels = [];
  List<VolumeLevel> get volumeLevels => _volumeLevels;

  List<VolumeLevel>? _filteredVolumeLevels;
  List<VolumeLevel>? get filteredVolumeLevels => _filteredVolumeLevels;

  bool _volumeFilterActive = false;
  bool get volumeFilterActive => _volumeFilterActive;

  List<StructureStorageItem> _structures = [];
  List<StructureStorageItem> get structures => _structures;

  List<StructureStorageItem> get structuresTimeFrameFilter =>
      _structures
          .where((s) => s.timeFrame == _currentConfig.timeFrame && s.symbol == _symbol)
          .toList();

  double? _lastUpBorder;
  double? _lastDownBorder;
  final List<StructureChangeItem> _structureChanges = [];
  List<StructureChangeItem> get structureChanges => _structureChanges;

  List<StructureChangeItem> get visibleStructureChanges => _structureChanges;

  List<PositionInfo> _positions = [];
  List<PositionInfo> get positions => _positions;

  List<OrderInfo> _orders = [];
  List<OrderInfo> get orders => _orders;

  List<HistoryDeal> _history = [];
  List<HistoryDeal> get history => _history;

  // --- Per-symbol configs ---
  final Map<String, SymbolConfig> _configs = {};

  SymbolConfig get _currentConfig =>
      _configs.putIfAbsent(_symbol, () => _loadConfigForSymbol(_symbol));

  int get timeFrame => _currentConfig.timeFrame;
  DateRangeMode get dateRangeMode => _currentConfig.dateRangeMode;
  int get lookbackDays => _currentConfig.lookbackDays;
  bool get bubbleVisible => _currentConfig.bubbleVisible;
  double get bubbleSize => _currentConfig.bubbleSize;
  double get bubbleOpacity => _currentConfig.bubbleOpacity;
  double get bubbleSizeMin => _currentConfig.bubbleSizeMin;
  double get bubbleSizeMax => _currentConfig.bubbleSizeMax;
  int get thresholdBubble => _currentConfig.thresholdBubble;

  bool get profileVisible => _currentConfig.profileVisible;
  double get profileSizeH => _currentConfig.profileSizeH;
  double get profileSizeV => _currentConfig.profileSizeV;
  double get profileOpacity => _currentConfig.profileOpacity;
  bool get profileAutoByPriceStructure => _currentConfig.profileAutoByPriceStructure;

  bool get structureVisible => _currentConfig.structureVisible;
  bool get structureAuxVisible => _currentConfig.structureAuxVisible;
  double get structureOpacity => _currentConfig.structureOpacity;
  /// Range INTRADAY (nunca o 1440: o diário vive em `dailyStructureRangeUpd`
  /// e trafega por rotas/histórico próprios).
  double get structureRangeUpd => _currentConfig.structureRangeUpd;

  String get colorBuyer => _currentConfig.colorBuyer;
  String get colorSeller => _currentConfig.colorSeller;

  List<int> get selectedAgents => _currentConfig.selectedAgents;
  Map<int, int> get agentThresholds => Map.unmodifiable(_currentConfig.agentThresholds);

  bool get bubbleAmountFilter => _currentConfig.bubbleAmountFilter;
  bool get bubbleAgentsFilter => _currentConfig.bubbleAgentsFilter;
  bool get bubbleSoundEnabled => _currentConfig.bubbleSoundEnabled;
  double get bubbleSoundVolume => _currentConfig.bubbleSoundVolume;

  bool get tradingHistoryVisible => _currentConfig.tradingHistoryVisible;
  bool get positionVisible => _currentConfig.positionVisible;
  bool get openOrdersVisible => _currentConfig.openOrdersVisible;

  bool get tradingPanelVisible => _currentConfig.tradingPanelVisible;
  bool get tradingConfigExpanded => _currentConfig.tradingConfigExpanded;
  bool get tradingAccountExpanded => _currentConfig.tradingAccountExpanded;
  bool get tradingOrdersExpanded => _currentConfig.tradingOrdersExpanded;
  bool get tradingPositionsExpanded => _currentConfig.tradingPositionsExpanded;
  bool get tradingHistoryExpanded => _currentConfig.tradingHistoryExpanded;

  bool _isStructureUpdating = false;
  bool get isStructureUpdating => _isStructureUpdating;

  // Data-driven (not persisted config)
  final Set<int> _allBubbleAgents = {};

  /// Agentes visíveis no drawer: os do dia carregado mais os já conhecidos
  /// e/ou selecionados (persistidos), para a lista não "sumir" ao trocar de dia.
  Set<int> get allBubbleAgents {
    if (_symbol.isEmpty) return _allBubbleAgents;
    return {
      ..._allBubbleAgents,
      ..._currentConfig.selectedAgents,
      ..._currentConfig.knownAgents,
    };
  }

  double _yZoom = 1.0;
  double get yZoom => _yZoom;

  bool get isConnected => _signalRService.isConnected;

  // Config setters
  // Navegação (símbolo/timeframe) NUNCA trava: só troca o que é exibido e
  // recarregado do servidor; os valores de filtro salvos seguem intactos e
  // a sessão armada continua usando a foto do PLAY. É o que permite abrir o
  // app com estratégia rodando e só olhar o gráfico (botão play superior).
  Future<void> setTimeFrame(int v) async {
    // Migração #12: 1D saiu do seletor principal (só intraday).
    if (v == 1440) v = 2;
    _currentConfig.timeFrame = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    await loadData();
  }
  void setBubbleVisible(bool v) { if (_guardLocked('setBubbleVisible')) return; _currentConfig.bubbleVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSize(double v) { if (_guardLocked('setBubbleSize')) return; _currentConfig.bubbleSize = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleOpacity(double v) { if (_guardLocked('setBubbleOpacity')) return; _currentConfig.bubbleOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSizeMin(double v) { if (_guardLocked('setBubbleSizeMin')) return; _currentConfig.bubbleSizeMin = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSizeMax(double v) { if (_guardLocked('setBubbleSizeMax')) return; _currentConfig.bubbleSizeMax = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setThresholdBubble(int v) { if (_guardLocked('setThresholdBubble')) return; _currentConfig.thresholdBubble = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setProfileVisible(bool v) { if (_guardLocked('setProfileVisible')) return; _currentConfig.profileVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileSizeH(double v) { if (_guardLocked('setProfileSizeH')) return; _currentConfig.profileSizeH = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileSizeV(double v) { if (_guardLocked('setProfileSizeV')) return; _currentConfig.profileSizeV = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileOpacity(double v) { if (_guardLocked('setProfileOpacity')) return; _currentConfig.profileOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileAutoByPriceStructure(bool v) {
    if (_guardLocked('setProfileAutoByPriceStructure')) return;
    _currentConfig.profileAutoByPriceStructure = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    if (v) {
      _applyStructureAutoFilter();
    } else {
      _applyVolumeFilter(0, barsTimeFrameFilter.length);
    }
  }

  void setStructureVisible(bool v) { if (_guardLocked('setStructureVisible')) return; _currentConfig.structureVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureAuxVisible(bool v) { if (_guardLocked('setStructureAuxVisible')) return; _currentConfig.structureAuxVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureOpacity(double v) { if (_guardLocked('setStructureOpacity')) return; _currentConfig.structureOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureRangeUpd(double v) { if (_guardLocked('setStructureRangeUpd')) return; _currentConfig.structureRangeUpd = v; _isStructureUpdating = true; notifyListeners(); _saveConfigForSymbol(_symbol); }

  // --- Análise diária (issue #12): configs escopadas ao widget diário ---
  /// Range DIÁRIO (só 1440). Independente de [structureRangeUpd] (intraday).
  double get dailyStructureRangeUpd => _currentConfig.daily.structureRangeUpd;
  bool get dailyStructureVisible => _currentConfig.daily.structureVisible;
  bool get dailyStructureAuxVisible =>
      _currentConfig.daily.structureAuxVisible;
  double get dailyStructureOpacity => _currentConfig.daily.structureOpacity;
  bool get dailyProfileVisible => _currentConfig.daily.profileVisible;
  bool get dailyProfileAutoByPriceStructure =>
      _currentConfig.daily.profileAutoByPriceStructure;
  double get dailyProfileSizeH => _currentConfig.daily.profileSizeH;
  double get dailyProfileSizeV => _currentConfig.daily.profileSizeV;
  double get dailyProfileOpacity => _currentConfig.daily.profileOpacity;
  bool get dailyPanelVisible => _currentConfig.daily.panelVisible;
  double get dailyPanelFraction => _currentConfig.daily.panelFraction;

  void setDailyPanelVisible(bool v) {
    if (_guardLocked('setDailyPanelVisible')) return;
    _currentConfig.daily.panelVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    if (v) {
      loadDailyAll();
    } else {
      _stopDailyLiveRefresh();
    }
  }

  void setDailyPanelFraction(double v) {
    if (_guardLocked('setDailyPanelFraction')) return;
    _currentConfig.daily.panelFraction = v.clamp(0.3, 0.7);
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  /// Preview durante o arrasto da divisória: atualiza a UI sem tocar o
  /// disco (evita uma rajada de writes por pixel arrastado). O valor é
  /// persistido em [commitDailyPanelFraction] ao fim do gesto.
  void previewDailyPanelFraction(double v) {
    _currentConfig.daily.panelFraction = v.clamp(0.3, 0.7);
    notifyListeners();
  }

  /// Persiste a fração atual (chamado ao fim do arrasto). Aguardado pelo
  /// chamador para que o valor sobreviva ao fechar o programa.
  Future<void> commitDailyPanelFraction() =>
      _saveConfigForSymbol(_symbol);

  void setDailyStructureVisible(bool v) {
    if (_guardLocked('setDailyStructureVisible')) return;
    _currentConfig.daily.structureVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setDailyStructureAuxVisible(bool v) {
    if (_guardLocked('setDailyStructureAuxVisible')) return;
    _currentConfig.daily.structureAuxVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setDailyStructureOpacity(double v) {
    if (_guardLocked('setDailyStructureOpacity')) return;
    _currentConfig.daily.structureOpacity = v.clamp(0.0, 1.0);
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setDailyProfileVisible(bool v) {
    if (_guardLocked('setDailyProfileVisible')) return;
    _currentConfig.daily.profileVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  /// Auto Mode do Volume 1D (paridade com o intraday): ligado = janela da
  /// última perna do 1440, recalculada a cada push de estrutura; desligado =
  /// janela manual do slider de período.
  void setDailyProfileAutoByPriceStructure(bool v) {
    if (_guardLocked('setDailyProfileAutoByPriceStructure')) return;
    _currentConfig.daily.profileAutoByPriceStructure = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    if (v) {
      _applyDailyStructureAutoFilter();
    } else {
      applyDailyVolumeFilterAndSyncExtremes(0, _dailyBars.length);
    }
  }

  void setDailyProfileSizeH(double v) {
    if (_guardLocked('setDailyProfileSizeH')) return;
    _currentConfig.daily.profileSizeH = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setDailyProfileSizeV(double v) {
    if (_guardLocked('setDailyProfileSizeV')) return;
    _currentConfig.daily.profileSizeV = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setDailyProfileOpacity(double v) {
    if (_guardLocked('setDailyProfileOpacity')) return;
    _currentConfig.daily.profileOpacity = v.clamp(0.0, 1.0);
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  // --- Filtro de período do Volume 1D (paridade com o intraday) ---
  // Índices sobre `_dailyBars` ordenados. Em auto-mode derivam da última
  // perna do 1440; em manual vêm do slider (só aplicados em onChangeEnd).
  int _dailyRangeStart = 0;
  int _dailyRangeEnd = 0;
  int get dailyRangeStart => _dailyRangeStart;
  int get dailyRangeEnd => _dailyRangeEnd;

  /// Aplica a janela manual do Volume 1D e recarrega perfil + topos juntos
  /// no mesmo from/to (paridade com `applyVolumeFilterAndSyncExtremes`).
  /// Chamado pelo slider diário em onChangeEnd (release do arrasto).
  Future<void> applyDailyVolumeFilterAndSyncExtremes(int start, int end) async {
    if (_guardLocked('applyDailyVolumeFilterAndSyncExtremes')) return;
    final count = _dailyBars.length;
    if (count == 0) return;
    _dailyRangeStart = start.clamp(0, count - 1);
    _dailyRangeEnd = end.clamp(_dailyRangeStart + 1, count);
    notifyListeners();
    final bars = List.of(_dailyBars)
      ..sort((a, b) => a.date.compareTo(b.date));
    DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);
    final from = dayOnly(bars[_dailyRangeStart].date);
    final lastIdx = (_dailyRangeEnd - 1).clamp(0, bars.length - 1);
    final to = dayOnly(bars[lastIdx].date);
    await loadDailyProfileAndExtremes(from: from, to: to);
  }

  /// Auto Mode 1D: janela = última perna do 1440 (última inversão de direção
  /// até o fim), recarregando perfil + topos. Espelho de
  /// `_applyStructureAutoFilter` sobre `dailyBars`/`structures1440History`.
  Future<void> _applyDailyStructureAutoFilter() async {
    final bars = List.of(_dailyBars)
      ..sort((a, b) => a.date.compareTo(b.date));
    if (bars.isEmpty) return;
    final (from, to) = resolveDailyAutoWindow();
    DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);
    var start = 0;
    for (int i = bars.length - 1; i >= 0; i--) {
      if (!dayOnly(bars[i].date).isAfter(from)) {
        start = i;
        break;
      }
    }
    _dailyRangeStart = start.clamp(0, bars.length - 1);
    _dailyRangeEnd = bars.length;
    logD('[dailyAuto] apply from=$from to=$to '
        'idx=$_dailyRangeStart..$_dailyRangeEnd bars=${bars.length} '
        'hist=${_structures1440History.length}');
    notifyListeners();
    await loadDailyProfileAndExtremes(from: from, to: to);
  }

  bool _isDailyStructureUpdating = false;
  bool get isDailyStructureUpdating => _isDailyStructureUpdating;
  // Recálculo automático do Range 1D: dispara só ao terminar a mudança
  // (onChangeEnd do slider) ou após debounce parado — nunca por pixel.
  Timer? _dailyStructureDebounce;
  bool _isDailyStructureConfirmRunning = false;
  bool get isDailyStructureConfirmRunning =>
      _isDailyStructureConfirmRunning;
  bool _dailyStructureConfirmPending = false;
  void setDailyStructureRangeUpd(double v) {
    if (_guardLocked('setDailyStructureRangeUpd')) return;
    _currentConfig.daily.structureRangeUpd = v;
    _isDailyStructureUpdating = true;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    // Debounce: se o usuário parar com o slider pressionado, confirma
    // sozinho; ao soltar, o onChangeEnd confirma de imediato (cancela aqui).
    _dailyStructureDebounce?.cancel();
    _dailyStructureDebounce =
        Timer(const Duration(milliseconds: 900), () {
      scheduleDailyStructureConfirm();
    });
  }

  /// Agenda o recálculo diário: `immediate=true` (fim do gesto) cancela o
  /// debounce e confirma na hora; senão, confirma se há mudança pendente.
  void scheduleDailyStructureConfirm({bool immediate = false}) {
    if (immediate) _dailyStructureDebounce?.cancel();
    if (_symbol.isEmpty || !_isDailyStructureUpdating) return;
    // ignore: discarded_futures
    confirmStructureRangeUpdDaily();
  }

  // Alias de compatibilidade com a seção diária da issue #10.
  double get structureRangeUpdDaily => dailyStructureRangeUpd;
  void setStructureRangeUpdDaily(double v) => setDailyStructureRangeUpd(v);

  /// Confirma a distância diária: regenera só o 1440 no servidor, recarrega
  /// o histórico 1440 e refaz perfil + extremos diários na janela atual
  /// (a âncora pode mudar com a nova distância). Guarda anti-concorrência:
  /// regen com janela longa é caro — se já há um em voo, o novo valor vira
  /// "pendente" (mais recente vence) em vez de paralelizar.
  Future<void> confirmStructureRangeUpdDaily() async {
    if (_symbol.isEmpty) return;
    if (_isDailyStructureConfirmRunning) {
      _dailyStructureConfirmPending = true;
      return;
    }
    _dailyStructureDebounce?.cancel();
    _isDailyStructureConfirmRunning = true;
    _isDailyStructureUpdating = true;
    notifyListeners();
    try {
      await _apiService.setStructureDistanceForTimeFrame(
          _symbol, 1440, _currentConfig.daily.structureRangeUpd);
      await refreshDailyStructureHistory();
      _dailyExtremes = null;
      _dailyProfileLevels = [];
      _dailyPivot = null;
      // Nova distância pode mover a última perna: re-deriva a janela
      // (auto) ou mantém os índices manuais (clamp na carga).
      if (_currentConfig.daily.profileAutoByPriceStructure) {
        await _applyDailyStructureAutoFilter();
      } else {
        _dailyRangeStart = 0;
        _dailyRangeEnd = _dailyBars.length;
        await loadDailyProfileAndExtremes();
      }
      await loadDailyPivot();
    } catch (e) {
      logD('[dailyStructure] confirm error: $e');
    } finally {
      _isDailyStructureConfirmRunning = false;
      // Sem pendência nova: mudança aplicada, limpa o flag (some o botão).
      // Com pendência: mantém o flag e roda de novo com o valor mais recente.
      if (_dailyStructureConfirmPending) {
        _dailyStructureConfirmPending = false;
        notifyListeners();
        await confirmStructureRangeUpdDaily();
      } else {
        _isDailyStructureUpdating = false;
        notifyListeners();
      }
    }
  }

  void setExtremeVisible(bool v) { if (_guardLocked('setExtremeVisible')) return; _currentConfig.extremeVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setExtremeOpacity(double v) { if (_guardLocked('setExtremeOpacity')) return; _currentConfig.extremeOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setExtremeNoiseSensitivity(double v) {
    if (_guardLocked('setExtremeNoiseSensitivity')) return;
    _currentConfig.extremeNoiseSensitivity = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _scheduleExtremeConfigSync();
  }
  void setExtremeMinimumProminence(double v) {
    if (_guardLocked('setExtremeMinimumProminence')) return;
    _currentConfig.extremeMinimumProminence = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _scheduleExtremeConfigSync();
  }

  // --- Análise diária: topos/vales (issue #12, escopados ao widget) ---
  bool get dailyExtremeVisible => _currentConfig.daily.extremeVisible;
  double get dailyExtremeOpacity => _currentConfig.daily.extremeOpacity;
  double get dailyExtremeNoiseSensitivity =>
      _currentConfig.daily.extremeNoiseSensitivity;
  double get dailyExtremeMinimumProminence =>
      _currentConfig.daily.extremeMinimumProminence;

  void setDailyExtremeVisible(bool v) {
    if (_guardLocked('setDailyExtremeVisible')) return;
    _currentConfig.daily.extremeVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }
  void setDailyExtremeOpacity(double v) {
    if (_guardLocked('setDailyExtremeOpacity')) return;
    _currentConfig.daily.extremeOpacity = v.clamp(0.0, 1.0);
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }
  void setDailyExtremeNoiseSensitivity(double v) {
    if (_guardLocked('setDailyExtremeNoiseSensitivity')) return;
    _currentConfig.daily.extremeNoiseSensitivity = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _scheduleDailyExtremeConfigSync();
  }
  void setDailyExtremeMinimumProminence(double v) {
    if (_guardLocked('setDailyExtremeMinimumProminence')) return;
    _currentConfig.daily.extremeMinimumProminence = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _scheduleDailyExtremeConfigSync();
  }

  // --- Pivot Tradicional diário (issue #14): só visible/opacity/lineCount ---
  bool get dailyPivotVisible => _currentConfig.daily.pivotVisible;
  double get dailyPivotOpacity => _currentConfig.daily.pivotOpacity;
  int get dailyPivotLineCount => _currentConfig.daily.pivotLineCount;

  void setDailyPivotVisible(bool v) {
    if (_guardLocked('setDailyPivotVisible')) return;
    _currentConfig.daily.pivotVisible = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }
  void setDailyPivotOpacity(double v) {
    if (_guardLocked('setDailyPivotOpacity')) return;
    _currentConfig.daily.pivotOpacity = v.clamp(0.0, 1.0);
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }
  void setDailyPivotLineCount(int v) {
    if (_guardLocked('setDailyPivotLineCount')) return;
    v = v.clamp(2, 5);
    if (_currentConfig.daily.pivotLineCount == v) return;
    _currentConfig.daily.pivotLineCount = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _scheduleDailyPivotSync();
  }

  Timer? _dailyPivotSyncTimer;

  /// Debounce do lineCount 1D (paridade com `_scheduleDailyExtremeConfigSync`):
  /// recarrega o pivot ~800ms após parar de arrastar — sem botão Atualizar,
  /// mudou a config já aparece no gráfico.
  void _scheduleDailyPivotSync() {
    _dailyPivotSyncTimer?.cancel();
    _dailyPivotSyncTimer = Timer(const Duration(milliseconds: 800), () {
      // ignore: discarded_futures
      loadDailyPivot();
    });
  }

  Timer? _dailyExtremeConfigTimer;

  /// Debounce do Noise/Prominence 1D (paridade com `_scheduleExtremeConfigSync`
  /// do intraday): recarrega os topos na janela vigente ~800ms após parar de
  /// arrastar. O botão Atualizar da aba continua como refresh forçado.
  void _scheduleDailyExtremeConfigSync() {
    _dailyExtremeConfigTimer?.cancel();
    _dailyExtremeConfigTimer = Timer(const Duration(milliseconds: 800), () {
      // ignore: discarded_futures
      loadDailyExtremes();
    });
  }

  void setVwapVisible(bool v) { if (_guardLocked('setVwapVisible')) return; _currentConfig.vwapVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setVwapOpacity(double v) { if (_guardLocked('setVwapOpacity')) return; _currentConfig.vwapOpacity = v.clamp(0.0, 1.0); notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setVwapColor(String v) { if (_guardLocked('setVwapColor')) return; _currentConfig.vwapColor = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setColorBuyer(String v) { if (_guardLocked('setColorBuyer')) return; _currentConfig.colorBuyer = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setColorSeller(String v) { if (_guardLocked('setColorSeller')) return; _currentConfig.colorSeller = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  /// Confirma o range INTRADAY: regenera só os timeframes < 1440 no
  /// servidor. Nunca toca no 1440 (que tem range próprio em
  /// `dailyStructureRangeUpd`); itens 1440 vindos na resposta são descartados.
  Future<void> confirmStructureRangeUpd() async {
    await setMinDistanceStructure(_currentConfig.structureRangeUpd);
    _isStructureUpdating = false;
    notifyListeners();
  }

  void setBubbleAmountFilter(bool v) { if (_guardLocked('setBubbleAmountFilter')) return; _currentConfig.bubbleAmountFilter = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleAgentsFilter(bool v) { if (_guardLocked('setBubbleAgentsFilter')) return; _currentConfig.bubbleAgentsFilter = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSoundEnabled(bool v) { _currentConfig.bubbleSoundEnabled = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSoundVolume(double v) { _currentConfig.bubbleSoundVolume = v.clamp(0.0, 1.0); notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setTradingHistoryVisible(bool v) { if (_guardLocked('setTradingHistoryVisible')) return; _currentConfig.tradingHistoryVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setPositionVisible(bool v) { if (_guardLocked('setPositionVisible')) return; _currentConfig.positionVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setOpenOrdersVisible(bool v) { if (_guardLocked('setOpenOrdersVisible')) return; _currentConfig.openOrdersVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setTradingPanelVisible(bool v) { if (_guardLocked('setTradingPanelVisible')) return; _currentConfig.tradingPanelVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingConfigExpanded(bool v) { if (_guardLocked('setTradingConfigExpanded')) return; _currentConfig.tradingConfigExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingAccountExpanded(bool v) { if (_guardLocked('setTradingAccountExpanded')) return; _currentConfig.tradingAccountExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingOrdersExpanded(bool v) { if (_guardLocked('setTradingOrdersExpanded')) return; _currentConfig.tradingOrdersExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingPositionsExpanded(bool v) { if (_guardLocked('setTradingPositionsExpanded')) return; _currentConfig.tradingPositionsExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingHistoryExpanded(bool v) { if (_guardLocked('setTradingHistoryExpanded')) return; _currentConfig.tradingHistoryExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setYZoom(double v) { _yZoom = v.clamp(0.3, 5.0); notifyListeners(); }

  Future<void> setDateRangeMode(DateRangeMode mode) async {
    if (_guardLocked('setDateRangeMode')) return;
    if (_currentConfig.dateRangeMode == mode) return;
    _currentConfig.dateRangeMode = mode;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    await loadData();
  }

  Future<void> setLookbackDays(int days) async {
    if (_guardLocked('setLookbackDays')) return;
    final clamped = days.clamp(1, 30);
    if (_currentConfig.lookbackDays == clamped) return;
    _currentConfig.lookbackDays = clamped;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    if (_currentConfig.dateRangeMode == DateRangeMode.multiDay) {
      await loadData();
    }
  }

  // --- Extreme detection ---
  bool get extremeVisible => _currentConfig.extremeVisible;
  double get extremeOpacity => _currentConfig.extremeOpacity;
  double get extremeNoiseSensitivity => _currentConfig.extremeNoiseSensitivity;
  double get extremeMinimumProminence => _currentConfig.extremeMinimumProminence;

  // --- Pivot Tradicional intraday (issue #14): só visible/opacity/lineCount ---
  bool get pivotVisible => _currentConfig.pivotVisible;
  double get pivotOpacity => _currentConfig.pivotOpacity;
  int get pivotLineCount => _currentConfig.pivotLineCount;

  void setPivotVisible(bool v) { if (_guardLocked('setPivotVisible')) return; _currentConfig.pivotVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setPivotOpacity(double v) { if (_guardLocked('setPivotOpacity')) return; _currentConfig.pivotOpacity = v.clamp(0.0, 1.0); notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setPivotLineCount(int v) {
    if (_guardLocked('setPivotLineCount')) return;
    v = v.clamp(2, 5);
    if (_currentConfig.pivotLineCount == v) return;
    _currentConfig.pivotLineCount = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    _schedulePivotIntradaySync();
  }

  Timer? _pivotIntradaySyncTimer;

  /// Debounce do lineCount intraday (paridade com `_scheduleDailyExtremeConfigSync`):
  /// recarrega o pivot ~800ms após parar de arrastar — sem botão Atualizar,
  /// mudou a config já aparece no gráfico.
  void _schedulePivotIntradaySync() {
    _pivotIntradaySyncTimer?.cancel();
    _pivotIntradaySyncTimer = Timer(const Duration(milliseconds: 800), () {
      // ignore: discarded_futures
      loadPivotIntraday();
    });
  }

  // --- VWAP ---
  bool get vwapVisible => _currentConfig.vwapVisible;
  double get vwapOpacity => _currentConfig.vwapOpacity;
  String get vwapColor => _currentConfig.vwapColor;

  /// Data efetiva em exibição (dia carregado no gráfico).
  DateTime? _displayDate;
  DateTime? get displayDate => _displayDate;

  /// Tela toda para rodar estratégias: o SymbolConfig vigente serializado,
  /// mesmo schema do export da aba Backup.
  Map<String, dynamic> get currentScreenConfig => _currentConfig.toJson();

  /// Trava de filtros com estratégia armada (rodar estratégias): enquanto
  /// houver sessão no backend, os setters de config recusam mudanças
  /// (PLAY congela a foto; para alterar: PAUSE/STOP, edita, PLAY de novo).
  /// Exceções de propósito (nunca travam): som (setBubbleSound*) e navegação
  /// (setSymbol/setTimeFrame) — são alerta local e troca de exibição, não
  /// alteram nenhum dado que a estratégia enxerga.
  bool _strategiesArmed = false;
  bool get strategiesArmed => _strategiesArmed;
  bool get configLocked => _strategiesArmed;
  void setStrategiesArmed(bool v) {
    _strategiesArmed = v;
    notifyListeners();
  }

  bool _guardLocked(String what) {
    if (!_strategiesArmed) return false;
    logD('[lock] $what bloqueado: pause a estratégia para alterar');
    return true;
  }

  // --- Sessões de estratégia (fonte única p/ aba + overlay do gráfico) ---
  // Poll compartilhado de 10s (gatilho típico é 2min); liga sob demanda e
  // fica ligado pelo tempo de vida do app. Aba e overlay consomem daqui.
  final StrategyService _strategyService;
  final bool _ownsStrategyService;
  List<StrategySession> _strategySessions = [];
  List<StrategySession> get strategySessions => _strategySessions;
  Timer? _strategyPollTimer;

  Future<void> refreshStrategySessions() async {
    try {
      final sessions = await _strategyService.state();
      _strategySessions = sessions;
      final armed = sessions.any((s) => !s.paused);
      if (strategiesArmed != armed) setStrategiesArmed(armed);
      notifyListeners();
    } catch (e) {
      logD('[strategy] refresh sessions error: $e');
    }
  }

  void ensureStrategyPolling() {
    if (_strategyPollTimer != null) return;
    _strategyPollTimer =
        Timer.periodic(const Duration(seconds: 10), (_) => refreshStrategySessions());
    refreshStrategySessions();
  }

  void _stopStrategyPolling() {
    _strategyPollTimer?.cancel();
    _strategyPollTimer = null;
  }

  // Opacidade do relatório sobre o gráfico (display local; sem guard de
  // propósito, como o som: não altera nenhum dado que a estratégia enxerga,
  // então pode ajustar com sessão rodando).
  double _strategyOverlayOpacity = 0.72;
  double get strategyOverlayOpacity => _strategyOverlayOpacity;
  void setStrategyOverlayOpacity(double v) {
    _strategyOverlayOpacity = v.clamp(0.01, 1.0);
    notifyListeners();
    _preferencesService.setDouble(
        'strategy_overlay_opacity', _strategyOverlayOpacity);
  }

  // Visibilidade do overlay por sessão (só display local; default visível).
  // Controlada na aba Estratégia (ícone de olho); não vai ao backend.
  final Set<String> _hiddenStrategySessions = {};
  bool isStrategySessionVisible(String sessionId) =>
      !_hiddenStrategySessions.contains(sessionId);
  void setStrategySessionVisible(String sessionId, bool visible) {
    if (visible) {
      _hiddenStrategySessions.remove(sessionId);
    } else {
      _hiddenStrategySessions.add(sessionId);
    }
    notifyListeners();
  }

  /// Range de datas carregado (para modo multi-day)
  DateTime? _rangeStartDate;
  DateTime? get rangeStartDate => _rangeStartDate;
  DateTime? _rangeEndDate;
  DateTime? get rangeEndDate => _rangeEndDate;

  ExtremeStorageItem? _extremes;
  ExtremeStorageItem? get extremes => _extremes;

  // Análise diária (issue #12): estado isolado do fluxo ao vivo/intraday.
  // Nunca é escrito por SignalR — só pelos loaders diários (lazy, ao abrir
  // o widget). Fonte do perfil: GetDailyProfile (agregado do backend).
  ExtremeStorageItem? _dailyExtremes;
  ExtremeStorageItem? get dailyExtremes => _dailyExtremes;
  bool _isDailyExtremeLoading = false;
  bool get isDailyExtremeLoading => _isDailyExtremeLoading;
  DateTime? _dailyExtremeFrom;
  DateTime? get dailyExtremeFrom => _dailyExtremeFrom;
  DateTime? _dailyExtremeTo;
  DateTime? get dailyExtremeTo => _dailyExtremeTo;

  // Pivot Tradicional (issue #14): snapshots estáticos do dia atual/último
  // pregão. Intraday usa fonte D-1, daily usa semana anterior (fiel ao
  // Profit). Isolados do fluxo ao vivo/SignalR, como os extremos diários.
  PivotStorageItem? _pivotIntraday;
  PivotStorageItem? get pivotIntraday => _pivotIntraday;
  bool _isPivotIntradayLoading = false;
  bool get isPivotIntradayLoading => _isPivotIntradayLoading;

  PivotStorageItem? _dailyPivot;
  PivotStorageItem? get dailyPivot => _dailyPivot;
  bool _isDailyPivotLoading = false;
  bool get isDailyPivotLoading => _isDailyPivotLoading;

  /// Histórico de estruturas 1440 (90 dias) — linhas do chart diário.
  /// Lista separada para não poluir a aba Estruturas.
  List<StructureStorageItem> _structures1440History = [];
  List<StructureStorageItem> get structures1440History =>
      List.unmodifiable(_structures1440History);

  /// Viradas de estrutura do 1440 (mais recentes primeiro, máx. 100).
  List<StructureChangeItem> get dailyStructureChanges {
    final daily = _structures1440History
        .where((s) => s.symbol == _symbol && s.timeFrame == 1440)
        .toList()
      ..sort((a, b) => a.date.compareTo(b.date));
    final changes = <StructureChangeItem>[];
    for (var i = 1; i < daily.length; i++) {
      final prev = daily[i - 1];
      final curr = daily[i];
      if (curr.upBorder != prev.upBorder) {
        changes.add(StructureChangeItem(
          date: curr.date,
          isUp: true,
          oldValue: prev.upBorder,
          newValue: curr.upBorder,
        ));
      }
      if (curr.downBorder != prev.downBorder) {
        changes.add(StructureChangeItem(
          date: curr.date,
          isUp: false,
          oldValue: prev.downBorder,
          newValue: curr.downBorder,
        ));
      }
    }
    return changes.reversed.take(100).toList();
  }

  /// Candles diários (TF 1440) do widget — fonte do chart 1D.
  List<BarStorageItem> _dailyBars = [];
  List<BarStorageItem> get dailyBars => List.unmodifiable(_dailyBars);
  bool _isDailyLoading = false;
  bool get isDailyLoading => _isDailyLoading;

  /// Níveis do Volume Profile diário (GetDailyProfile, fonte 100% diária).
  List<VolumeLevel> _dailyProfileLevels = [];
  List<VolumeLevel> get dailyProfileLevels =>
      List.unmodifiable(_dailyProfileLevels);
  bool _isDailyProfileLoading = false;
  bool get isDailyProfileLoading => _isDailyProfileLoading;
  DateTime? _dailyProfileFrom;
  DateTime? get dailyProfileFrom => _dailyProfileFrom;
  DateTime? _dailyProfileTo;
  DateTime? get dailyProfileTo => _dailyProfileTo;

  /// Mantém a barra diária de hoje viva em tempo real: reagrega o OHLCV a
  /// partir do intraday mais fino disponível, para que o last price do
  /// split diário ande junto com o gráfico principal. Não notifica —
  /// todos os chamadores já notificam em seguida.
  void _refreshTodayDailyBar() {
    if (_dailyBars.isEmpty || _symbol.isEmpty) return;
    final now = DateTime.now();
    bool sameDay(DateTime d) =>
        d.year == now.year && d.month == now.month && d.day == now.day;
    final todays = _bars.where((b) => sameDay(b.date)).toList();
    if (todays.isEmpty) return;
    // _bars contém todos os timeframes do dia: usa só o mais fino para
    // não somar o volume múltiplas vezes.
    final tf = todays.map((b) => b.timeFrame).reduce(min);
    final bars = todays.where((b) => b.timeFrame == tf).toList()
      ..sort((a, b) => a.date.compareTo(b.date));
    if (bars.isEmpty) return;
    final live = BarStorageItem(
      date: DateTime(now.year, now.month, now.day),
      symbol: _symbol,
      timeFrame: 1440,
      open: bars.first.open,
      high: bars.map((b) => b.high).reduce(max),
      low: bars.map((b) => b.low).reduce(min),
      close: bars.last.close,
      volume: bars.fold<int>(0, (p, b) => p + b.volume),
    );
    final idx = _dailyBars.indexWhere((b) => sameDay(b.date));
    if (idx >= 0) {
      _dailyBars[idx] = live;
    } else {
      _dailyBars.add(live);
      _dailyBars.sort((a, b) => a.date.compareTo(b.date));
    }
  }

  /// Recarrega o histórico 1440 com a distância diária atual.
  Future<void> refreshDailyStructureHistory() async {
    if (_symbol.isEmpty) return;
    try {
      final hist = await _apiService.getStructureHistory(
          _symbol, 1440, _currentConfig.daily.structureRangeUpd,
          days: 90);
      hist.sort((a, b) => a.date.compareTo(b.date));
      _structures1440History = hist;
      logD(
          '[dailyStructure] history 1440 count=${hist.length} dist=${_currentConfig.daily.structureRangeUpd}');
    } catch (e) {
      logD('[dailyStructure] history error: $e');
    }
  }

  Timer? _extremeConfigTimer;
  Timer? _extremePeriodTimer;
  DateTime? _lastExtremeFrom;
  DateTime? _lastExtremeTo;

  /// Filtra snapshots que não correspondem ao dia exibido: o broadcast ao vivo
  /// (dia atual do servidor) não pode sobrescrever os extremos de uma data
  /// histórica, e vice-versa.
  void _handleExtreme(ExtremeStorageItem data) {
    if (data.symbol.isNotEmpty && data.symbol != _symbol) {
      logD('[extremes] reject symbol=${data.symbol} != $_symbol');
      return;
    }
    final d = data.date;
    if (d != null) {
      if (_currentConfig.dateRangeMode == DateRangeMode.multiDay) {
        if (_rangeStartDate != null && _rangeEndDate != null) {
          final dateOnly = DateTime(d.year, d.month, d.day);
          final startOnly = DateTime(_rangeStartDate!.year, _rangeStartDate!.month, _rangeStartDate!.day);
          final endOnly = DateTime(_rangeEndDate!.year, _rangeEndDate!.month, _rangeEndDate!.day);
          if (dateOnly.isBefore(startOnly) || dateOnly.isAfter(endOnly)) {
            logD('[extremes] reject date=$dateOnly outside range $_rangeStartDate to $_rangeEndDate');
            return;
          }
        }
      } else {
        final disp = _displayDate;
        if (disp != null &&
            (d.year != disp.year || d.month != disp.month || d.day != disp.day)) {
          logD(
              '[extremes] reject date=${d.toIso8601String()} != display=${disp.toIso8601String()}');
          return;
        }
      }
    }
    logD(
        '[extremes] apply date=${d?.toIso8601String()} count=${data.extremes.length}');
    _extremes = data;
    notifyListeners();
  }

  (DateTime?, DateTime?) _extremeRangeFromBars() {
    final bars = barsTimeFrameFilter;
    final count = bars.length;
    if (count == 0) return (null, null);
    final start = _dateRangeStart;
    final end = _dateRangeEnd;
    final from = start <= 0 ? null : bars[start - 1].date;
    final endIdx = end.clamp(0, count - 1);
    final to = end >= count ? null : bars[endIdx].date;
    return (from, to);
  }

  void _scheduleExtremeConfigSync() {
    _extremeConfigTimer?.cancel();
    _extremeConfigTimer = Timer(const Duration(milliseconds: 800), () async {
      try {
        final range = _extremeRangeFromBars();
        final data = await _apiService.setExtremeConfig(
          _symbol,
          date: _displayDate,
          from: range.$1,
          to: range.$2,
          noiseSensitivity: _currentConfig.extremeNoiseSensitivity,
          minimumProminence: _currentConfig.extremeMinimumProminence,
        );
        if (data != null) _handleExtreme(data);
      } catch (e) {
        logD('[extremes] config sync error: $e');
      }
    });
  }

  /// Debounce: durante o arrasto acumula o último from/to e envia uma única
  /// requisição ~350ms após a última mudança, garantindo que a posição final
  /// do slider sempre dispare (sem o lag do throttle de 2s anterior).
  void _syncExtremesPeriod({bool immediate = false}) {
    if (_displayDate == null || _symbol.isEmpty) return;

    if (immediate) {
      _extremePeriodTimer?.cancel();
      _doSyncExtremesPeriod(true);
      return;
    }

    _extremePeriodTimer?.cancel();
    _extremePeriodTimer = Timer(const Duration(milliseconds: 350), () => _doSyncExtremesPeriod(false));
  }

  void _doSyncExtremesPeriod(bool immediate) async {
    final (from, to) = _extremeRangeFromBars();

    if (_lastExtremeFrom == from && _lastExtremeTo == to) return;
    _lastExtremeFrom = from;
    _lastExtremeTo = to;

    final url = _apiService.setExtremePeriodDebugUrl(
        _symbol, date: _displayDate, from: from, to: to);
    logD(
        '[extremes] syncPeriod from=$from to=$to url=$url display=${_displayDate?.toIso8601String()} immediate=$immediate');
    try {
      final data = await _apiService
          .setExtremePeriod(_symbol, date: _displayDate, from: from, to: to);
      logD(
          '[extremes] syncPeriod response ${data == null ? 'NULL' : 'count=${data.extremes.length}'}');
      if (data != null) _handleExtreme(data);
    } catch (e) {
      logD('[extremes] period sync error: $e');
    }
  }

  /// Aplica o filtro de volume E sincroniza os extremos imediatamente (sem debounce).
  /// Chamado pelo TimeRangeSlider no onChangeEnd (release do arrasto).
  void applyVolumeFilterAndSyncExtremes(int start, int end) {
    if (_guardLocked('applyVolumeFilterAndSyncExtremes')) return;
    _applyVolumeFilter(start, end);
    _syncExtremesPeriod(immediate: true);
  }

  // --- Daily overlay: auto-mode + carga on-demand (issue #10) ---
  // O auto-mode replica o conceito do intraday sobre o histórico de
  // estruturas 1440 com a distância diária: a última inversão de direção
  // define o início da "última perna" do diário.
  (DateTime, DateTime) resolveDailyAutoWindow() {
    final now = DateTime.now();
    final to = DateTime(now.year, now.month, now.day);
    DateTime from = to.subtract(const Duration(days: 60));
    try {
      final daily = _structures1440History
          .where((s) => s.symbol == _symbol && s.timeFrame == 1440)
          .toList()
        ..sort((a, b) => a.date.compareTo(b.date));
      if (daily.length >= 2) {
        final changes = <StructureChangeItem>[];
        for (var i = 1; i < daily.length; i++) {
          final prev = daily[i - 1];
          final curr = daily[i];
          if (curr.upBorder != prev.upBorder) {
            changes.add(StructureChangeItem(
              date: curr.date,
              isUp: true,
              oldValue: prev.upBorder,
              newValue: curr.upBorder,
            ));
          }
          if (curr.downBorder != prev.downBorder) {
            changes.add(StructureChangeItem(
              date: curr.date,
              isUp: false,
              oldValue: prev.downBorder,
              newValue: curr.downBorder,
            ));
          }
        }
        if (changes.isNotEmpty) {
          changes.sort((a, b) => b.date.compareTo(a.date));
          final last = changes.first;
          StructureChangeItem? anchor;
          var anchorPos = -1;
          for (var k = 0; k < changes.length; k++) {
            final c = changes[k];
            if (c.isUp != last.isUp && c.isUpMove != last.isUpMove) {
              anchor = c;
              anchorPos = k;
              break;
            }
          }
          // Início da perna = dia que fez o extremo (aux), não o dia que
          // confirmou por distanciamento. Trigger continua na confirmação.
          final bars = List.of(_dailyBars)
            ..sort((a, b) => a.date.compareTo(b.date));
          if (anchor != null) {
            // lowerDay = confirmação do lado oposto mais recente antes da
            // âncora (ponto de reset do aux; ver intraday). A mudança
            // imediatamente anterior pode ser same-side e cortar o E real.
            final resetDate = oppositeResetDate(changes, anchorPos);
            final lowerDay = resetDate != null
                ? DateTime(resetDate.year, resetDate.month, resetDate.day)
                : null;
            from = resolveDailyAnchorDay(
              dailyBars: bars,
              history: _structures1440History,
              symbol: _symbol,
              anchor: anchor,
              lowerDay: lowerDay,
            );
          } else {
            final firstChange = changes.last;
            from = resolveDailyAnchorDay(
              dailyBars: bars,
              history: _structures1440History,
              symbol: _symbol,
              anchor: firstChange,
            );
          }
        }
      }
    } catch (e) {
      logD('[dailyExtremes] anchor fallback: $e');
    }
    if (from.isAfter(to)) {
      from = to.subtract(const Duration(days: 60));
    }
    return (from, to);
  }

  /// Janela vigente do widget diário: auto-mode = última perna do 1440;
  /// manual = índices do slider sobre `_dailyBars`.
  (DateTime, DateTime) resolveDailyWindow() {
    if (_currentConfig.daily.profileAutoByPriceStructure) {
      return resolveDailyAutoWindow();
    }
    final bars = List.of(_dailyBars)
      ..sort((a, b) => a.date.compareTo(b.date));
    if (bars.isEmpty) return resolveDailyAutoWindow();
    DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);
    final s = _dailyRangeStart.clamp(0, bars.length - 1);
    final e = _dailyRangeEnd.clamp(s + 1, bars.length);
    return (
      dayOnly(bars[s].date),
      dayOnly(bars[(e - 1).clamp(0, bars.length - 1)].date)
    );
  }

  /// Carga completa do widget diário (lazy, ao abrir o sheet): histórico
  /// 1440 → janela → candles 1440 + perfil agregado + extremos.
  /// Ao final, liga o refresh vivo (perfil + topos juntos a cada 45s)
  /// para que o volume do pregão em andamento entre no 1D.
  Future<void> loadDailyAll() async {
    if (_symbol.isEmpty || _isDailyLoading) return;
    _isDailyLoading = true;
    notifyListeners();
    try {
      await refreshDailyStructureHistory();
      final now = DateTime.now();
      final barsTo = DateTime(now.year, now.month, now.day);
      final barsFrom = barsTo.subtract(const Duration(days: 120));
      try {
        final bars =
            await _apiService.getBarRange(_symbol, barsFrom, barsTo, 1440);
        bars.sort((a, b) => a.date.compareTo(b.date));
        _dailyBars = bars;
        // Se o pregão de hoje já tem intraday carregado, a barra de hoje
        // já nasce viva (last price atual desde a abertura do painel).
        _refreshTodayDailyBar();
        logD('[daily] bars 1440 count=${bars.length}');
      } catch (e) {
        logD('[daily] bars error: $e');
      }
      // Sincroniza os índices da sombra com a janela vigente: auto deriva
      // da última perna do 1440, manual cobre tudo até o slider mexer.
      if (_currentConfig.daily.profileAutoByPriceStructure) {
        await _applyDailyStructureAutoFilter();
      } else {
        _dailyRangeStart = 0;
        _dailyRangeEnd = _dailyBars.length;
        await loadDailyProfileAndExtremes();
      }
      // Pivot Tradicional diário (fonte semana anterior, issue #14):
      // independe da janela do perfil — carga própria, mesmo padrão lazy.
      await loadDailyPivot();
    } finally {
      _isDailyLoading = false;
      notifyListeners();
      _startDailyLiveRefresh();
    }
  }

  /// Refresh vivo do 1D: recarrega perfil + extremos juntos a cada 45s,
  /// só com o painel aberto e hoje dentro da janela carregada. Reusa o
  /// from/to armazenados para não deslocar a sombra do gráfico durante
  /// o pregão. Pula o ciclo se já houver carga/Confirm em voo.
  Timer? _dailyLiveTimer;
  void _startDailyLiveRefresh() {
    _dailyLiveTimer?.cancel();
    if (_symbol.isEmpty || !_currentConfig.daily.panelVisible) return;
    _dailyLiveTimer =
        Timer.periodic(const Duration(seconds: 45), (_) => _onDailyLiveTick());
  }

  void _stopDailyLiveRefresh() {
    _dailyLiveTimer?.cancel();
    _dailyLiveTimer = null;
  }

  void _onDailyLiveTick() {
    if (_symbol.isEmpty) return;
    if (!_currentConfig.daily.panelVisible) return;
    final pf = _dailyProfileFrom;
    final pt = _dailyProfileTo;
    if (pf == null || pt == null) return;
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final f = DateTime(pf.year, pf.month, pf.day);
    final t = DateTime(pt.year, pt.month, pt.day);
    // Janela histórica sem hoje: nada muda no vivo, não recarrega.
    if (today.isBefore(f) || today.isAfter(t)) return;
    if (_isDailyLoading ||
        _isDailyProfileLoading ||
        _isDailyExtremeLoading ||
        _isDailyStructureConfirmRunning ||
        _isDailyStructureUpdating) {
      return;
    }
    logD('[dailyLive] tick refresh from=$pf to=$pt');
    // ignore: discarded_futures
    loadDailyProfileAndExtremes(from: pf, to: pt);
  }

  /// Recarrega perfil + extremos diários na janela vigente — auto-mode
  /// (última perna do 1440) ou índices manuais —, com o mesmo from/to para
  /// os dois, garantindo coerência entre barras de volume e linhas.
  /// `from/to` explícitos (slider em onChangeEnd) têm precedência.
  Future<void> loadDailyProfileAndExtremes(
      {DateTime? from, DateTime? to}) async {
    if (_symbol.isEmpty) return;
    final (wFrom, wTo) = resolveDailyWindow();
    final f = from ?? wFrom;
    final t = to ?? wTo;
    await Future.wait([
      loadDailyProfile(from: f, to: t),
      loadDailyExtremes(from: f, to: t),
    ]);
  }

  /// Perfil agregado diário (fonte 100% diária: GetDailyProfile).
  Future<void> loadDailyProfile({DateTime? from, DateTime? to}) async {
    if (_symbol.isEmpty || _isDailyProfileLoading) return;
    _isDailyProfileLoading = true;
    notifyListeners();
    try {
      final (wFrom, wTo) = resolveDailyWindow();
      final f = from ?? wFrom;
      final t = to ?? wTo;
      logD('[dailyProfile] load $_symbol from=$f to=$t');
      final levels =
          await _apiService.getDailyProfile(_symbol, from: f, to: t);
      levels.sort((a, b) => a.price.compareTo(b.price));
      _dailyProfileLevels = levels;
      _dailyProfileFrom = f;
      _dailyProfileTo = t;
      logD('[dailyProfile] applied levels=${levels.length}');
    } catch (e) {
      logD('[dailyProfile] load error: $e');
    } finally {
      _isDailyProfileLoading = false;
      notifyListeners();
    }
  }

  /// Carga dos topos/vales diários. Recarrega em mudança de estrutura
  /// (auto-mode), no slider manual (onChangeEnd), no debounce de
  /// Noise/Prominence, pelo botão "Atualizar" da aba e no refresh vivo
  /// de 45s (junto com o perfil, mesmo from/to).
  Future<void> loadDailyExtremes({bool force = true, DateTime? from, DateTime? to}) async {
    if (_symbol.isEmpty || _isDailyExtremeLoading) return;
    _isDailyExtremeLoading = true;
    notifyListeners();
    try {
      final (wFrom, wTo) = resolveDailyWindow();
      final f = from ?? wFrom;
      final t = to ?? wTo;
      logD('[dailyExtremes] load $_symbol from=$f to=$t '
          'noise=${_currentConfig.daily.extremeNoiseSensitivity} '
          'prom=${_currentConfig.daily.extremeMinimumProminence}');
      final data = await _apiService.getExtremeDaily(
        _symbol,
        from: f,
        to: t,
        noiseSensitivity: _currentConfig.daily.extremeNoiseSensitivity,
        minimumProminence: _currentConfig.daily.extremeMinimumProminence,
      );
      if (data != null) {
        // Isolamento: valida símbolo; ignora filtro de data intraday de propósito
        // (janela multi-dia nunca coincidiria com _displayDate de 1 dia).
        if (data.symbol.isEmpty || data.symbol == _symbol) {
          _dailyExtremes = data;
          _dailyExtremeFrom = f;
          _dailyExtremeTo = t;
          logD('[dailyExtremes] applied count=${data.extremes.length}');
        }
      }
    } catch (e) {
      logD('[dailyExtremes] load error: $e');
    } finally {
      _isDailyExtremeLoading = false;
      notifyListeners();
    }
  }

  void clearDailyExtremes() {
    _dailyExtremes = null;
    _dailyExtremeFrom = null;
    _dailyExtremeTo = null;
    notifyListeners();
  }

  /// Carga do Pivot Tradicional intraday (fonte D-1). Reativa: carrega no
  /// loadData e recarrega sozinha no debounce do lineCount — sem botão
  /// Atualizar, mudou a config já aparece no gráfico.
  Future<void> loadPivotIntraday() async {
    if (_symbol.isEmpty || _isPivotIntradayLoading) return;
    _isPivotIntradayLoading = true;
    notifyListeners();
    try {
      logD('[pivot] intraday load $_symbol lines=${_currentConfig.pivotLineCount}');
      final data = await _apiService.getPivotIntraday(
        _symbol,
        lineCount: _currentConfig.pivotLineCount,
      );
      if (data != null) {
        if (data.symbol.isEmpty || data.symbol == _symbol) {
          _pivotIntraday = data;
          logD('[pivot] intraday applied levels=${data.levels.length} source=${data.source}');
        }
      }
    } catch (e) {
      logD('[pivot] intraday load error: $e');
    } finally {
      _isPivotIntradayLoading = false;
      notifyListeners();
    }
  }

  void clearPivotIntraday() {
    _pivotIntraday = null;
    notifyListeners();
  }

  /// Carga do Pivot Tradicional diário (fonte semana anterior). Reativa:
  /// carrega no loadDailyAll e recarrega sozinha no debounce do lineCount —
  /// sem botão Atualizar, mudou a config já aparece no gráfico.
  Future<void> loadDailyPivot() async {
    if (_symbol.isEmpty || _isDailyPivotLoading) return;
    _isDailyPivotLoading = true;
    notifyListeners();
    try {
      logD('[pivot] daily load $_symbol lines=${_currentConfig.daily.pivotLineCount}');
      final data = await _apiService.getPivotDaily(
        _symbol,
        lineCount: _currentConfig.daily.pivotLineCount,
      );
      if (data != null) {
        if (data.symbol.isEmpty || data.symbol == _symbol) {
          _dailyPivot = data;
          logD('[pivot] daily applied levels=${data.levels.length} source=${data.source}');
        }
      }
    } catch (e) {
      logD('[pivot] daily load error: $e');
    } finally {
      _isDailyPivotLoading = false;
      notifyListeners();
    }
  }

  void clearDailyPivot() {
    _dailyPivot = null;
    notifyListeners();
  }

  void clearDailyProfile() {
    _dailyProfileLevels = [];
    _dailyProfileFrom = null;
    _dailyProfileTo = null;
    notifyListeners();
  }

  void selectAllAgents() {
    if (_guardLocked('selectAllAgents')) return;
    final all = allBubbleAgents;
    if (_currentConfig.selectedAgents.length == all.length) {
      _currentConfig.selectedAgents = [];
    } else {
      _currentConfig.selectedAgents = all.toList();
    }
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void toggleAgent(int agent) {
    if (_guardLocked('toggleAgent')) return;
    if (_currentConfig.selectedAgents.contains(agent)) {
      _currentConfig.selectedAgents.remove(agent);
    } else {
      _currentConfig.selectedAgents.add(agent);
    }
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setAgentThreshold(int agent, int? threshold) {
    if (_guardLocked('setAgentThreshold')) return;
    if (threshold != null) {
      _currentConfig.agentThresholds[agent] = threshold;
    } else {
      _currentConfig.agentThresholds.remove(agent);
    }
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  int getThreshold(int? agent) {
    if (agent == null) return 0;
    return _currentConfig.agentThresholds[agent] ?? _currentConfig.thresholdBubble;
  }

  // --- Per-symbol persistence ---
  SymbolConfig? _legacy;

  /// Snapshot único (por sessão) das configs globais antigas, para serem
  /// aplicadas como seed a todo símbolo sem config própria.
  SymbolConfig? get _legacyConfig =>
      _legacy ??= _tryMigrateFromOldKeys();

  SymbolConfig _loadConfigForSymbol(String symbol) {
    final json = _preferencesService.getString('Config_$symbol');
    if (json != null) {
      try {
        final config = SymbolConfig.fromJson(jsonDecode(json), symbol: symbol);
        logD('[config] load $symbol -> noise=${config.extremeNoiseSensitivity} '
            'prominence=${config.extremeMinimumProminence} '
            'visible=${config.extremeVisible} opacity=${config.extremeOpacity}');
        return config;
      } catch (_) {}
    }

    final legacy = _legacyConfig;
    if (legacy != null) {
      final config = SymbolConfig.seedFromLegacy(symbol, legacy);
      _configs[symbol] = config;
      _saveConfigForSymbol(symbol);
      return config;
    }

    return SymbolConfig.withDefaults(symbol);
  }

  SymbolConfig? _tryMigrateFromOldKeys() {
    final p = _preferencesService;
    if (p.getInt('TimeFrame') == null) return null;

    var legacyTimeFrame = p.getInt('TimeFrame') ?? 2;
    // Migração #12: 1D saiu do seletor principal.
    if (legacyTimeFrame == 1440) legacyTimeFrame = 2;
    return SymbolConfig(
      timeFrame: legacyTimeFrame,
      dateRangeMode: DateRangeMode.intraday,
      lookbackDays: 5,
      thresholdBubble: p.getInt('ThresholdBubble') ?? 250,
      structureRangeUpd: p.getDouble('StructureRangeUpd') ?? 250,
      daily: DailyAnalysisConfig.withDefaults(''),
      structureVisible: p.getBool('StructureVisible') ?? true,
      structureAuxVisible: p.getBool('StructureAuxVisible') ?? true,
      structureOpacity: p.getDouble('StructureOpacity') ?? 0.8,
      extremeVisible: true,
      extremeOpacity: 0.7,
      extremeNoiseSensitivity: 3.0,
      extremeMinimumProminence: 0.15,
      pivotVisible: true,
      pivotOpacity: 0.7,
      pivotLineCount: 2,
      vwapVisible: true,
      vwapOpacity: 0.5,
      vwapColor: '#FF8800',
      bubbleSize: p.getDouble('BubbleSize') ?? 1.0,
      bubbleOpacity: p.getDouble('BubbleOpacity') ?? 0.7,
      bubbleVisible: p.getBool('BubbleVisible') ?? true,
      bubbleSizeMin: p.getDouble('BubbleSizeMin') ?? 20,
      bubbleSizeMax: p.getDouble('BubbleSizeMax') ?? 100,
      profileSizeH: p.getDouble('ProfileSizeH') ?? 1.0,
      profileSizeV: p.getDouble('ProfileSizeV') ?? 1.0,
      profileOpacity: p.getDouble('ProfileOpacity') ?? 0.5,
      profileVisible: p.getBool('ProfileVisible') ?? true,
      profileAutoByPriceStructure:
          p.getBool('ProfileAutoByPriceStructure') ?? false,
      colorBuyer: p.getString('ColorBuyer') ?? '#4488ff',
      colorSeller: p.getString('ColorSeller') ?? '#ff4444',
      selectedAgents: p.getIntList('SelectedAgents') ?? [],
      agentThresholds: p.getIntIntMap('AgentThresholds') ?? {},
      knownAgents: [],
      bubbleAmountFilter: p.getBool('BubbleAmountFilter') ?? true,
      bubbleAgentsFilter: p.getBool('BubbleAgentsFilter') ?? true,
      bubbleSoundEnabled: p.getBool('BubbleSoundEnabled') ?? true,
      bubbleSoundVolume: p.getDouble('BubbleSoundVolume') ?? 0.5,
      tradingHistoryVisible: true,
      positionVisible: true,
      openOrdersVisible: true,
      tradingPanelVisible: true,
      tradingConfigExpanded: false,
      tradingAccountExpanded: false,
      tradingOrdersExpanded: false,
      tradingPositionsExpanded: false,
      tradingHistoryExpanded: false,
    );
  }
  Future<void> _saveConfigForSymbol(String symbol) async {
    if (symbol.isEmpty || !_configs.containsKey(symbol)) return;
    final config = _configs[symbol]!;
    logD('[config] save $symbol -> noise=${config.extremeNoiseSensitivity} '
        'prominence=${config.extremeMinimumProminence} '
        'visible=${config.extremeVisible} opacity=${config.extremeOpacity}');
    await _preferencesService.setString(
        'Config_$symbol', jsonEncode(config.toJson()));
  }

  // --- Backup: export/import de todas as configs (issue backup) ---
  // O arquivo contém um [ConfigBackup]: mapa símbolo -> SymbolConfig
  // (payload idêntico ao `Config_$symbol`) + `activeSymbol`.
  // Import usa semântica "substituir tudo": símbolos locais fora do
  // arquivo são removidos do disco.

  /// Símbolos conhecidos: persistidos no disco + em memória.
  List<String> knownConfigSymbols() {
    final set = <String>{
      ..._preferencesService.getConfigSymbols(),
      ..._configs.keys.where((s) => s.isNotEmpty),
    };
    final out = set.toList()..sort();
    return out;
  }

  /// Monta o backup com todas as configs (descarrega a atual primeiro).
  Future<ConfigBackup> exportAllConfigs() async {
    await _saveConfigForSymbol(_symbol);
    for (final s in _preferencesService.getConfigSymbols()) {
      if (!_configs.containsKey(s)) {
        _configs[s] = _loadConfigForSymbol(s);
      }
    }
    final snapshot = <String, SymbolConfig>{
      for (final e in _configs.entries)
        if (e.key.isNotEmpty)
          e.key: SymbolConfig.fromJson(e.value.toJson(), symbol: e.key),
    };
    logD('[backup] export symbols=${snapshot.keys.toList()} '
        'active=$_symbol');
    return ConfigBackup(
      activeSymbol: _symbol,
      symbols: snapshot,
    );
  }

  /// Valida e aplica o backup, persistindo tudo e recarregando o símbolo
  /// ativo (pré-seleciona agentes, thresholds e demais configs nos drawers).
  /// Lança [FormatException] sem alterar nada se o JSON for inválido.
  /// A recarga de dados (`setSymbol`) pode falhar offline — nesse caso as
  /// configs já estão aplicadas/persistidas e o erro é propagado.
  Future<void> importAllConfigs(Map<String, dynamic> json) async {
    final backup = ConfigBackup.fromJson(json);
    final stale = knownConfigSymbols()
        .where((s) => !backup.symbols.containsKey(s))
        .toList();
    for (final s in stale) {
      _configs.remove(s);
      await _preferencesService.remove('Config_$s');
    }
    _configs
      ..clear()
      ..addAll({
        for (final e in backup.symbols.entries)
          e.key: SymbolConfig.fromJson(e.value.toJson(), symbol: e.key),
      });
    for (final s in _configs.keys) {
      await _saveConfigForSymbol(s);
    }
    final target =
        backup.activeSymbol.isNotEmpty && _configs.containsKey(backup.activeSymbol)
            ? backup.activeSymbol
            : _configs.keys.first;
    logD('[backup] import symbols=${_configs.keys.toList()} '
        'active=$target removed=$stale');
    await setSymbol(target);
  }

  // --- Process Loop ---
  Timer? _processTimer;
  Timer? _watchdogTimer;
  DateTime _lastChartUpdate = DateTime.now();
  bool _chartGenInProgress = false;

  static const int maxBubbles = 2000;

  void _init() {
    _strategyOverlayOpacity =
        _preferencesService.getDouble('strategy_overlay_opacity') ?? 0.72;
    _signalRService.onCloseBar = _handleCloseBar;
    _signalRService.onNewBubble = _handleNewBubble;
    _signalRService.onVolumeUpdate = _handleVolumeUpdate;
    _signalRService.onCurrentBar = _handleCurrentBar;
    _signalRService.onDailyBar = _handleDailyBar;
    _signalRService.onNewStructure = _handleNewStructure;
    _signalRService.onMissedBars = _handleMissedBars;
    _signalRService.onMissedBubbles = _handleMissedBubbles;
    _signalRService.onExtreme = _handleExtreme;
  }

  Future<void> setSymbol(String value) async {
    // Sem guard de propósito (ver comentário em setTimeFrame): trocar de
    // símbolo é navegação e recarrega a tela; não altera filtros salvos.
    _saveConfigForSymbol(_symbol);
    _stopDailyLiveRefresh();
    _symbol = value.toUpperCase();
    _allBubbleAgents.clear();
    _extremes = null;
    _pivotIntraday = null;
    _dailyExtremes = null;
    _dailyExtremeFrom = null;
    _dailyExtremeTo = null;
    _dailyBars = [];
    _dailyRangeStart = 0;
    _dailyRangeEnd = 0;
    _dailyProfileLevels = [];
    _dailyProfileFrom = null;
    _dailyProfileTo = null;
    _dailyPivot = null;
    _structures1440History = [];
    _displayDate = null;
    _lastExtremeFrom = null;
    _lastExtremeTo = null;
    notifyListeners();
    await loadData();
  }

  Future<void> loadData() async {
    if (_symbol.isEmpty) return;

    _isLoading = true;
    notifyListeners();

    try {
      if (_currentConfig.dateRangeMode == DateRangeMode.intraday) {
        await _loadIntraday();
      } else {
        await _loadMultiDay();
      }
    } finally {
      _isLoading = false;

      await _signalRService.startConnection(_symbol, _currentConfig.timeFrame);
      // For multi-day, refresh structures for the end date (today)
      final refreshDate = _currentConfig.dateRangeMode == DateRangeMode.intraday
          ? _displayDate!
          : _rangeEndDate!;
      await _refreshStructuresAfterConnection(refreshDate);
      _startProcessLoop();
      _startWatchdog();
      _scheduleExtremeConfigSync();
      // Análise diária (issue #12) é lazy: só carrega ao abrir o widget
      // (loadDailyAll), sem custo/background no fluxo intraday. Se o painel
      // já estava aberto (troca de símbolo/timeframe), mantém a barra de
      // hoje viva com o intraday recém-carregado.
      _refreshTodayDailyBar();
      notifyListeners();
    }
  }

  Future<void> _loadIntraday() async {
    var effectiveDate = DateTime.now();

    try {
      logD('[loadData] Trying today: ${_formatDate(effectiveDate)}');

      var bars = await _apiService.getBars(_symbol, effectiveDate);
      logD('[loadData] Today bars count: ${bars.length}');
      logD('[loadData] Available timeframes: ${bars.map((b) => b.timeFrame).toSet()}');

      if (bars.isEmpty) {
        effectiveDate = await _apiService.findLastDateWithData(_symbol);
        logD('[loadData] Falling back to date: ${_formatDate(effectiveDate)}');
        bars = await _apiService.getBars(_symbol, effectiveDate);
        logD('[loadData] Fallback bars count: ${bars.length}');
      }

      _bars = bars;
      _displayDate = effectiveDate;
      _rangeStartDate = _rangeEndDate = effectiveDate;
      notifyListeners();

      final filteredCount = barsTimeFrameFilter.length;
      logD('[loadData] _timeFrame=${_currentConfig.timeFrame}, filteredCount=$filteredCount, totalBars=${_bars.length}');

      _dateRangeStart = 0;
      _dateRangeEnd = filteredCount;
      _volumeFilterActive = false;
      _filteredVolumeLevels = null;

      // Load bubbles
      final bubbles = await _apiService.getBubbles(_symbol, effectiveDate);
      _bubbles = bubbles;
      _allBubbleAgents.clear();
      var knownChanged = false;
      for (final b in bubbles) {
        _allBubbleAgents.add(b.agent);
        if (!_currentConfig.knownAgents.contains(b.agent)) {
          _currentConfig.knownAgents.add(b.agent);
          _currentConfig.selectedAgents.add(b.agent);
          knownChanged = true;
        }
      }
      if (knownChanged) _saveConfigForSymbol(_symbol);
      // Mantém agentes conhecidos/selecionados visíveis mesmo sem bubble na
      // data carregada, para a seleção não "sumir" ao trocar de dia.
      _allBubbleAgents.addAll(_currentConfig.selectedAgents);
      _allBubbleAgents.addAll(_currentConfig.knownAgents);
      logD('[loadData] Bubbles count: ${bubbles.length}');

      // Load volume
      final volumeData = await _apiService.getVolume(_symbol, effectiveDate);
      if (volumeData != null && volumeData.volumes.isNotEmpty) {
        _volumeLevels = volumeData.volumes;
        logD('[loadData] Volume levels loaded: ${_volumeLevels.length}');
      } else if (_bars.isNotEmpty) {
        final barsWithVol = _bars
            .where((b) => b.volumeLevel != null && b.volumeLevel!.isNotEmpty)
            .toList();
        logD('[loadData] Bars with volumeLevel: ${barsWithVol.length}');
        if (barsWithVol.isNotEmpty) {
          _volumeLevels = barsWithVol.reduce(
            (a, b) => a.date.compareTo(b.date) > 0 ? a : b).volumeLevel ?? [];
          logD('[loadData] Volume from last bar: ${_volumeLevels.length}');
        }
      }

      if (_volumeLevels.isNotEmpty) {
        // Range completo no load: perfil cumulativo ao vivo (sem filtro).
        _filteredVolumeLevels = null;
        _volumeFilterActive = false;
      }
      notifyListeners();

      // Load structure
      final structures = await _fetchStructures(effectiveDate);
      _structures = structures;
      _recomputeStructureChanges(structures);
      logD('[loadData] Structures count: ${structures.length}');
      if (_currentConfig.profileAutoByPriceStructure) {
        _applyStructureAutoFilter();
      }
      notifyListeners();

      // Load extreme detection snapshot
      try {
        final range = _extremeRangeFromBars();
        final extreme = await _apiService.getExtreme(
          _symbol,
          effectiveDate,
          from: range.$1,
          to: range.$2,
        );
        if (extreme != null) {
          _extremes = extreme;
          logD(
              '[loadData] Extremes loaded: ${extreme.extremes.length} (from=${range.$1} to=${range.$2})');
        }
      } catch (e) {
        logD('[loadData] Extreme load error: $e');
      }
      // Pivot Tradicional intraday (fonte D-1, issue #14): snapshot estático
      // da sessão, independente dos extremos.
      try {
        await loadPivotIntraday();
      } catch (e) {
        logD('[loadData] Pivot load error: $e');
      }
      notifyListeners();

    } catch (e) {
      logD('[loadData] Error: $e');
      logD('[loadData] Stack: ${StackTrace.current}');
    }
  }

  Future<void> _loadMultiDay() async {
    final endDate = DateTime.now();
    final startDate = endDate.subtract(Duration(days: _currentConfig.lookbackDays - 1));
    _rangeStartDate = startDate;
    _rangeEndDate = endDate;
    _displayDate = endDate;  // for compatibility

    try {
      logD('[loadData] Multi-day: ${_formatDate(startDate)} to ${_formatDate(endDate)}');

      // Parallel loads (intraday: o range 1440 nunca entra em `_structures`;
      // o 1440 vive em `_structures1440History` com o range diário próprio).
      final bars = await _apiService.getBarRange(_symbol, startDate, endDate, _currentConfig.timeFrame);
      final bubbles = await _apiService.getBubbleRange(_symbol, startDate, endDate);
      final structures = await _apiService.getStructureRange(_symbol, startDate, endDate, _currentConfig.structureRangeUpd);

      _bars = bars..sort((a, b) => a.date.compareTo(b.date));
      _bubbles = bubbles..sort((a, b) => a.date.compareTo(b.date));
      _structures =
          structures.where((s) => s.timeFrame != 1440).toList();
      _recomputeStructureChanges(_structures);

      // Collect all bubble agents
      _allBubbleAgents.clear();
      var knownChanged = false;
      for (final b in _bubbles) {
        _allBubbleAgents.add(b.agent);
        if (!_currentConfig.knownAgents.contains(b.agent)) {
          _currentConfig.knownAgents.add(b.agent);
          _currentConfig.selectedAgents.add(b.agent);
          knownChanged = true;
        }
      }
      if (knownChanged) _saveConfigForSymbol(_symbol);
      _allBubbleAgents.addAll(_currentConfig.selectedAgents);
      _allBubbleAgents.addAll(_currentConfig.knownAgents);

      logD('[loadData] Multi-day bars: ${_bars.length}, bubbles: ${_bubbles.length}, structures: ${_structures.length}');

      // Volume: cumulative from last day or computed from bars
      await _loadCumulativeVolume(startDate, endDate);

      // Extremes: range API
      await _loadExtremesForRange(startDate, endDate);

      // Pivot Tradicional intraday (fonte D-1, issue #14): snapshot estático
      // da sessão atual, independente do range multi-day.
      try {
        await loadPivotIntraday();
      } catch (e) {
        logD('[loadData] Pivot load error: $e');
      }

      // Full range for volume filter
      _dateRangeStart = 0;
      _dateRangeEnd = _bars.length;
      _volumeFilterActive = false;
      _filteredVolumeLevels = null;
      notifyListeners();

    } catch (e) {
      logD('[loadData] Multi-day error: $e');
      logD('[loadData] Stack: ${StackTrace.current}');
    }
  }

  Future<void> _loadCumulativeVolume(DateTime start, DateTime end) async {
    // Try last day's snapshot first
    final lastDayVolume = await _apiService.getVolume(_symbol, end);
    if (lastDayVolume != null && lastDayVolume.volumes.isNotEmpty) {
      _volumeLevels = lastDayVolume.volumes;
      logD('[loadData] Volume from last day: ${_volumeLevels.length}');
    } else if (_bars.isNotEmpty) {
      // Fallback: compute from bars' volumeLevel snapshots
      final barsWithVol = _bars
          .where((b) => b.volumeLevel != null && b.volumeLevel!.isNotEmpty)
          .toList();
      if (barsWithVol.isNotEmpty) {
        _volumeLevels = barsWithVol.reduce(
          (a, b) => a.date.compareTo(b.date) > 0 ? a : b).volumeLevel ?? [];
        logD('[loadData] Volume from last bar with vol: ${_volumeLevels.length}');
      }
    }
    notifyListeners();
  }

  Future<void> _loadExtremesForRange(DateTime start, DateTime end) async {
    try {
      final extreme = await _apiService.getExtreme(
        _symbol,
        end,  // use end date as reference
        from: start,
        to: end,
      );
      if (extreme != null) {
        _extremes = extreme;
        logD('[loadData] Extremes loaded: ${extreme.extremes.length} (from=$start to=$end)');
      }
    } catch (e) {
      logD('[loadData] Extreme range load error: $e');
    }
  }

  Future<List<StructureStorageItem>> _fetchStructures(
    DateTime date, {
    bool allowRetry = true,
  }) async {
    // Intraday: descarta eventual 1440 da resposta (o diário tem lista,
    // range e rotas próprios e nunca mora em `_structures`).
    List<StructureStorageItem> sanitize(List<StructureStorageItem> src) =>
        src.where((s) => s.timeFrame != 1440).toList();
    var result = sanitize(await _apiService.getStructure(
        _symbol, date, _currentConfig.structureRangeUpd));
    for (var attempt = 0;
        allowRetry && result.isEmpty && attempt < 4;
        attempt++) {
      logD(
          '[loadData] Structures empty on attempt ${attempt + 1}, retrying...');
      await Future.delayed(const Duration(seconds: 2));
      result = sanitize(await _apiService.getStructure(
          _symbol, date, _currentConfig.structureRangeUpd));
    }
    return result;
  }

  Future<void> _refreshStructuresAfterConnection(DateTime date) async {
    try {
      final fetched = await _fetchStructures(date, allowRetry: false);
      if (fetched.isEmpty) return;

      final keys = <String>{
        for (final s in _structures)
          '${s.timeFrame}|${s.date.toIso8601String()}',
      };
      var merged = false;
      for (final s in fetched) {
        final key = '${s.timeFrame}|${s.date.toIso8601String()}';
        if (!keys.contains(key)) {
          _structures.add(s);
          keys.add(key);
          merged = true;
        }
      }
      if (merged) {
        _recomputeStructureChanges(_structures);
        logD('[loadData] Merged ${fetched.length} structures after connection');
      }
    } catch (e) {
      logD('[loadData] Structure refresh after connection error: $e');
    }
  }

  String _formatDate(DateTime d) =>
      '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  /// Checks if a date is within the loaded range (for multi-day mode)
  /// In intraday mode, always returns true
  bool _isInLoadedRange(DateTime date) {
    if (_currentConfig.dateRangeMode == DateRangeMode.intraday) return true;
    if (_rangeStartDate == null || _rangeEndDate == null) return false;
    final dateOnly = DateTime(date.year, date.month, date.day);
    final startOnly = DateTime(_rangeStartDate!.year, _rangeStartDate!.month, _rangeStartDate!.day);
    final endOnly = DateTime(_rangeEndDate!.year, _rangeEndDate!.month, _rangeEndDate!.day);
    // Allow today's live data even if outside range
    final now = DateTime.now();
    final todayOnly = DateTime(now.year, now.month, now.day);
    if (dateOnly == todayOnly) return true;
    return !dateOnly.isBefore(startOnly) && !dateOnly.isAfter(endOnly);
  }

  void _startProcessLoop() {
    _processTimer?.cancel();
    _processLoop();
  }

  void _processLoop() async {
    while (_signalRService.started) {
      final loopStart = DateTime.now();
      try {
        await Future.delayed(Duration(milliseconds: _throttlingDelayMs));

        if (!_chartGenInProgress) {
          _chartGenInProgress = true;
          _onChartUpdate();
          _chartGenInProgress = false;
          _lastChartUpdate = DateTime.now();
        }
      } catch (e) {
        logD('ProcessLoop error: $e');
      }

      final elapsed = DateTime.now().difference(loopStart);
      if (elapsed.inMilliseconds < _throttlingDelayMs) {
        await Future.delayed(
            Duration(milliseconds: _throttlingDelayMs - elapsed.inMilliseconds));
      }
    }
  }

  void _startWatchdog() {
    _watchdogTimer?.cancel();
    _watchdogTimer = Timer.periodic(const Duration(seconds: 5), (_) async {
      final elapsed = DateTime.now().difference(_lastChartUpdate);
      if (elapsed.inSeconds > 10) {
        logD(
            '[WATCHDOG] Chart sem update há ${elapsed.inSeconds}s - recovery');

        if (_signalRService.started && !_signalRService.isConnected) {
          await _signalRService.startConnection(_symbol, _currentConfig.timeFrame);
        }
      }
    });
  }

  // --- SignalR Handlers ---

  void _handleCloseBar(BarStorageItem bar) {
    if (bar.date == DateTime(0)) return;
    if (!_isInLoadedRange(bar.date)) return;

    final existingIndex =
        _bars.indexWhere((b) => b.date == bar.date && b.timeFrame == bar.timeFrame);
    if (existingIndex >= 0) {
      final existing = _bars[existingIndex];
      _bars[existingIndex] = _preserveVolumeLevel(bar, existing);
    } else {
      _bars.add(bar);
    }

    final filteredCount = barsTimeFrameFilter.length;
    if (filteredCount > 0 && _dateRangeEnd >= filteredCount - 1) {
      _dateRangeEnd = filteredCount;
      _applyVolumeFilter(_dateRangeStart, _dateRangeEnd);
    }
    _refreshTodayDailyBar();

    notifyListeners();
  }

  void _handleCurrentBar(BarStorageItem bar) {
    if (!_isInLoadedRange(bar.date)) return;
    final existingIndex =
        _bars.indexWhere((b) => b.date == bar.date && b.timeFrame == bar.timeFrame);
    if (existingIndex >= 0) {
      final existing = _bars[existingIndex];
      bar = _preserveVolumeLevel(bar, existing);
      _bars[existingIndex] = bar;
    } else {
      _bars.add(bar);
    }
    _currentBar = bar;

    final filteredCount = barsTimeFrameFilter.length;
    if (filteredCount > 0 && _dateRangeEnd >= filteredCount - 1) {
      _dateRangeEnd = filteredCount;
      _applyVolumeFilter(_dateRangeStart, _dateRangeEnd);
    }
    _refreshTodayDailyBar();

    notifyListeners();
  }

  /// Barra diária ao vivo (snapshot 1440 do throttling, 4x/s): upsert na
  /// lista do split diário, mesmo caminho de atualização do 2min.
  /// Ignora snapshots vazios (serviço recém-iniciado, Date 0001-01-01) e
  /// nada faz com o painel fechado e sem histórico (lazy da issue #12).
  void _handleDailyBar(BarStorageItem bar) {
    if (bar.timeFrame != 1440 || bar.date.year < 2020) return;
    if (_symbol.isEmpty) return;
    if (_dailyBars.isEmpty && !_currentConfig.daily.panelVisible) return;
    final day = DateTime(bar.date.year, bar.date.month, bar.date.day);
    final live = BarStorageItem(
      date: day,
      symbol: _symbol,
      timeFrame: 1440,
      open: bar.open,
      high: bar.high,
      low: bar.low,
      close: bar.close,
      volume: bar.volume,
    );
    final idx = _dailyBars.indexWhere((b) =>
        b.date.year == day.year &&
        b.date.month == day.month &&
        b.date.day == day.day);
    if (idx >= 0) {
      _dailyBars[idx] = live;
    } else {
      _dailyBars.add(live);
      _dailyBars.sort((a, b) => a.date.compareTo(b.date));
    }
    notifyListeners();
  }

  BarStorageItem _preserveVolumeLevel(
      BarStorageItem incoming, BarStorageItem existing) {
    if ((incoming.volumeLevel == null || incoming.volumeLevel!.isEmpty) &&
        (existing.volumeLevel != null && existing.volumeLevel!.isNotEmpty)) {
      return incoming.copyWith(volumeLevel: existing.volumeLevel);
    }
    return incoming;
  }

  void _handleNewBubble(BubbleStorageItem data) {
    if (!_isInLoadedRange(data.date)) return;
    // Auto-seleciona apenas na primeira aparição do agente. Se o usuário
    // desmarcar depois, não será reselecionado ao reaparecer em outro dia.
    if (!_currentConfig.knownAgents.contains(data.agent)) {
      _currentConfig.knownAgents.add(data.agent);
      _currentConfig.selectedAgents.add(data.agent);
      _saveConfigForSymbol(_symbol);
    }
    _allBubbleAgents.add(data.agent);
    _bubbles.add(data);
    if (_bubbles.length > maxBubbles) {
      _bubbles.removeRange(0, _bubbles.length - maxBubbles);
    }
    if (bubbleSoundEnabled && _bubblePassesFilters(data)) {
      _audioService.playNotification(volume: bubbleSoundVolume);
    }
    notifyListeners();
  }

  bool _bubblePassesFilters(BubbleStorageItem b) {
    if (bubbleAmountFilter) {
      final threshold = getThreshold(b.agent);
      if (b.amount < threshold) return false;
    }
    if (bubbleAgentsFilter) {
      if (!selectedAgents.contains(b.agent)) return false;
    }
    return true;
  }

  void _handleVolumeUpdate(VolumeLevelStorageItem volumes) {
    // Volume updates are for current day - check if today is in range
    final today = DateTime.now();
    final todayOnly = DateTime(today.year, today.month, today.day);
    if (!_isInLoadedRange(todayOnly)) return;
    _volumeLevels = volumes.volumes;

    if (barsTimeFrameFilter.isNotEmpty) {
      _applyVolumeFilter(_dateRangeStart, _dateRangeEnd);
    }

    notifyListeners();
  }

  void _handleNewStructure(StructureStorageItem structure) {
    // Diário (1440) chega via push em eventos discretos (virada do dia ou
    // fim do regen do Confirm): upsert no histórico 1440. O D0 no chart é
    // forward-fill daqui (ver buildDailyChartData) — estático, sem recalcular
    // a cada tick.
    if (structure.timeFrame == 1440) {
      if (structure.symbol != _symbol || structure.date.year < 2020) return;
      final day = DateTime(
          structure.date.year, structure.date.month, structure.date.day);
      final item = StructureStorageItem(
        date: day,
        symbol: structure.symbol,
        timeFrame: 1440,
        upBorder: structure.upBorder,
        downBorder: structure.downBorder,
        upAuxBorder: structure.upAuxBorder,
        downAuxBorder: structure.downAuxBorder,
      );
      final idx = _structures1440History.indexWhere((s) =>
          s.symbol == _symbol &&
          s.timeFrame == 1440 &&
          s.date.year == day.year &&
          s.date.month == day.month &&
          s.date.day == day.day);
      if (idx >= 0) {
        _structures1440History[idx] = item;
      } else {
        _structures1440History.add(item);
        _structures1440History.sort((a, b) => a.date.compareTo(b.date));
      }
      notifyListeners();
      // Paridade com o superior (`_handleNewStructure` intraday só move o
      // filtro em auto-mode): estrutura nova move perfil + topos 1D só com
      // auto ligado e só se a janela derivada mudou (estáticos por janela).
      // Nunca durante um Confirm em voo: o sync terminal do Confirm (com
      // histórico recém-recarregado) é autoritativo — sem isso o push
      // (histórico velho) dispara loads que bloqueiam o reload novo.
      if (_currentConfig.daily.profileAutoByPriceStructure &&
          !_isDailyLoading &&
          !_isDailyStructureUpdating &&
          !_isDailyStructureConfirmRunning &&
          !_isDailyProfileLoading &&
          !_isDailyExtremeLoading) {
        final (from, to) = resolveDailyAutoWindow();
        if (_dailyProfileFrom != from || _dailyProfileTo != to) {
          logD('[dailyAuto] push 1440 re-anchor from=$from to=$to');
          // ignore: discarded_futures
          _applyDailyStructureAutoFilter();
        }
      }
      return;
    }
    if (structure.symbol != _symbol || structure.timeFrame != _currentConfig.timeFrame) return;
    if (!_isInLoadedRange(structure.date)) return;
    _structures.add(structure);
    if (_lastUpBorder != null && structure.upBorder != _lastUpBorder) {
      _structureChanges.insert(0, StructureChangeItem(
        date: structure.date,
        isUp: true,
        oldValue: _lastUpBorder!,
        newValue: structure.upBorder,
      ));
    }
    if (_lastDownBorder != null && structure.downBorder != _lastDownBorder) {
      _structureChanges.insert(0, StructureChangeItem(
        date: structure.date,
        isUp: false,
        oldValue: _lastDownBorder!,
        newValue: structure.downBorder,
      ));
    }
    _lastUpBorder = structure.upBorder;
    _lastDownBorder = structure.downBorder;
    if (_currentConfig.profileAutoByPriceStructure) {
      _applyStructureAutoFilter();
    }
    notifyListeners();
  }

  void _recomputeStructureChanges(List<StructureStorageItem> structures) {
    _structureChanges.clear();
    final relevant = structures
        .where((s) => s.symbol == _symbol && s.timeFrame == _currentConfig.timeFrame)
        .toList();
    if (relevant.length < 2) {
      if (relevant.isNotEmpty) {
        _lastUpBorder = relevant.last.upBorder;
        _lastDownBorder = relevant.last.downBorder;
      }
      return;
    }
    final sorted = List<StructureStorageItem>.from(relevant)
      ..sort((a, b) => a.date.compareTo(b.date));
    for (int i = 1; i < sorted.length; i++) {
      final prev = sorted[i - 1];
      final curr = sorted[i];
      if (curr.upBorder != prev.upBorder) {
        _structureChanges.add(StructureChangeItem(
          date: curr.date,
          isUp: true,
          oldValue: prev.upBorder,
          newValue: curr.upBorder,
        ));
      }
      if (curr.downBorder != prev.downBorder) {
        _structureChanges.add(StructureChangeItem(
          date: curr.date,
          isUp: false,
          oldValue: prev.downBorder,
          newValue: curr.downBorder,
        ));
      }
    }
    _lastUpBorder = sorted.last.upBorder;
    _lastDownBorder = sorted.last.downBorder;
    final reversed = _structureChanges.reversed.toList();
    _structureChanges
      ..clear()
      ..addAll(reversed);
  }

  void _handleMissedBars(List<BarStorageItem> bars) {
    for (final bar in bars) {
      _handleCloseBar(bar);
    }
  }

  void _handleMissedBubbles(List<BubbleStorageItem> bubbles) {
    for (final b in bubbles) {
      _handleNewBubble(b);
    }
  }

  // --- Volume Filter ---
  int _dateRangeStart = 0;
  int _dateRangeEnd = 0;
  int get dateRangeStart => _dateRangeStart;
  int get dateRangeEnd => _dateRangeEnd;
  int get barsCount => barsTimeFrameFilter.length;

  void applyVolumeFilter(int start, int end) {
    _applyVolumeFilter(start, end);
  }

  /// Índice do candle de confirmação: última barra com `date <= changeDate`.
  static int confirmationBarIndex(
      List<BarStorageItem> bars, DateTime changeDate) {
    var idx = 0;
    for (int i = bars.length - 1; i >= 0; i--) {
      if (!bars[i].date.isAfter(changeDate)) {
        idx = i;
        break;
      }
    }
    return idx;
  }

  /// Data da mudança de lado oposto mais recente antes de `anchorPos`
  /// (`changesDesc` em ordem mais-recente-primeiro). É o ponto de reset do
  /// aux da âncora no backend — o extremo E é sempre posterior a ela.
  /// Null quando não há: busca sem limite inferior. Pular a mudança
  /// imediatamente anterior (que pode ser same-side) é o que evita cortar
  /// o próprio E e deslocar o início do filtro 1 candle para frente.
  static DateTime? oppositeResetDate(
      List<StructureChangeItem> changesDesc, int anchorPos) {
    if (anchorPos < 0 || anchorPos >= changesDesc.length) return null;
    final anchorIsUp = changesDesc[anchorPos].isUp;
    for (var k = anchorPos + 1; k < changesDesc.length; k++) {
      if (changesDesc[k].isUp != anchorIsUp) return changesDesc[k].date;
    }
    return null;
  }

  /// Retrocede do candle de confirmação ao candle do PRIMEIRO toque no
  /// extremo (E): o candle que marcou a alteração do aux e que de fato fez o
  /// topo/fundo. O candle E é INCLUSO no filtro: `_computeWindowVolume`
  /// subtrai o snapshot de `start - 1`, logo a janela cobre `[E..end]`.
  /// Por quê E e não o último toque (R)? O Diff é
  /// `cumulativo(fim) − cumulativo(start−1)`; com start=R, todo o volume dos
  /// candles `E..R−1` é cancelado na subtração — os primeiros candles da
  /// perna somem do perfil por construção. Com start=E a perna inteira entra.
  /// Fonte primária = série aux das impressões de estrutura (a mesma que
  /// desenha a tracejada: primeiro print da perna cujo aux alcançou o valor
  /// confirmado); fallback = varredura de `high`/`low` nas barras de frente
  /// para trás (topo `isUp` casa `high == newValue`, fundo casa
  /// `low == newValue`); último fallback = confirmação (comportamento antigo).
  /// O trigger continua na confirmação — só o início do filtro volta a E.
  static int resolveExtremeBarIndex({
    required List<BarStorageItem> bars,
    required List<StructureStorageItem> structures,
    required String symbol,
    required int timeFrame,
    required StructureChangeItem anchor,
    required int confirmationIndex,
    int lowerBound = 0,
    required double tolerance,
  }) {
    if (bars.isEmpty) return 0;
    final conf = confirmationIndex.clamp(0, bars.length - 1);
    final lo = lowerBound.clamp(0, conf);
    final prints = structures
        .where((s) => s.symbol == symbol && s.timeFrame == timeFrame)
        .toList()
      ..sort((a, b) => a.date.compareTo(b.date));
    if (prints.isNotEmpty) {
      final loDate = bars[lo].date;
      for (final p in prints) {
        if (p.date.isBefore(loDate) || p.date.isAfter(anchor.date)) continue;
        final aux = anchor.isUp ? p.upAuxBorder : p.downAuxBorder;
        if ((aux - anchor.newValue).abs() <= tolerance) {
          return confirmationBarIndex(bars, p.date);
        }
      }
    }
    for (int i = lo; i <= conf; i++) {
      final price = anchor.isUp ? bars[i].high : bars[i].low;
      if ((price - anchor.newValue).abs() <= tolerance) return i;
    }
    return conf;
  }

  /// Versão diária (barras 1440, comparação por dia): retrocede do dia de
  /// confirmação ao dia do PRIMEIRO toque no extremo (incluso na janela,
  /// pelo mesmo motivo do intraday: com o último toque os primeiros dias da
  /// perna seriam cancelados no agregado).
  /// `lowerDay` limita a busca à perna vigente (dia de confirmação da
  /// mudança antecessora, se houver).
  static DateTime resolveDailyAnchorDay({
    required List<BarStorageItem> dailyBars,
    required List<StructureStorageItem> history,
    required String symbol,
    required StructureChangeItem anchor,
    DateTime? lowerDay,
  }) {
    DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);
    final confirmationDay = dayOnly(anchor.date);
    final tolerance = Defaults.tickSize(symbol) / 2;
    final prints = history
        .where((s) => s.symbol == symbol && s.timeFrame == 1440)
        .toList()
      ..sort((a, b) => a.date.compareTo(b.date));
    if (prints.isNotEmpty) {
      final loDay = lowerDay != null ? dayOnly(lowerDay) : null;
      for (final p in prints) {
        final d = dayOnly(p.date);
        if (d.isAfter(confirmationDay)) break;
        if (loDay != null && d.isBefore(loDay)) continue;
        final aux = anchor.isUp ? p.upAuxBorder : p.downAuxBorder;
        if ((aux - anchor.newValue).abs() <= tolerance) return d;
      }
    }
    if (dailyBars.isEmpty) return confirmationDay;
    final sorted = List.of(dailyBars)
      ..sort((a, b) => a.date.compareTo(b.date));
    var confIdx = 0;
    for (int i = sorted.length - 1; i >= 0; i--) {
      if (!dayOnly(sorted[i].date).isAfter(confirmationDay)) {
        confIdx = i;
        break;
      }
    }
    var loIdx = 0;
    if (lowerDay != null) {
      final loDay = dayOnly(lowerDay);
      for (int i = confIdx; i >= 0; i--) {
        if (dayOnly(sorted[i].date).isBefore(loDay)) {
          loIdx = (i + 1).clamp(0, confIdx);
          break;
        }
      }
    }
    for (int i = loIdx; i <= confIdx; i++) {
      final price = anchor.isUp ? sorted[i].high : sorted[i].low;
      if ((price - anchor.newValue).abs() <= tolerance) {
        return dayOnly(sorted[i].date);
      }
    }
    return confirmationDay;
  }

  void _applyStructureAutoFilter() {
    final bars = barsTimeFrameFilter;
    if (bars.isEmpty) return;
    final end = bars.length;
    if (_structureChanges.isEmpty) {
      _applyVolumeFilter(0, end);
      return;
    }
    final lastChange = _structureChanges.first;
    StructureChangeItem? anchor;
    var anchorPos = -1;
    for (var k = 0; k < _structureChanges.length; k++) {
      final c = _structureChanges[k];
      if (c.isUp != lastChange.isUp && c.isUpMove != lastChange.isUpMove) {
        anchor = c;
        anchorPos = k;
        break;
      }
    }
    if (anchor == null) {
      _applyVolumeFilter(0, end);
      return;
    }
    // Candle de confirmação (comportamento antigo = trigger do update).
    final confirmation = confirmationBarIndex(bars, anchor.date);
    // Limite inferior = confirmação da mudança de lado OPOSTO mais recente
    // antes da âncora (ponto de reset do aux no backend: o aux da âncora
    // acumula desde lá). Usar a mudança imediatamente anterior cortaria o
    // próprio E quando há uma mudança same-side no meio (ex.: dois topos
    // seguidos) — a sombra começava 1 candle depois do extremo real.
    // Também impede atravessar duas pernas quando o preço se repete longe.
    final resetDate = oppositeResetDate(_structureChanges, anchorPos);
    var lowerBound = 0;
    if (resetDate != null) {
      lowerBound = confirmationBarIndex(bars, resetDate);
    }
    // Início do filtro = candle do PRIMEIRO toque no extremo (E, incluso no
    // perfil), não o que confirmou.
    final start = resolveExtremeBarIndex(
      bars: bars,
      structures: _structures,
      symbol: _symbol,
      timeFrame: _currentConfig.timeFrame,
      anchor: anchor,
      confirmationIndex: confirmation,
      lowerBound: lowerBound,
      tolerance: Defaults.tickSize(_symbol) / 2,
    ).clamp(0, end - 1);
    _applyVolumeFilter(start, end);
  }

  void _applyVolumeFilter(int start, int end) {
    _dateRangeStart = start;
    _dateRangeEnd = end;

    final count = barsTimeFrameFilter.length;
    if (count == 0) return;

    final filtered = _computeWindowVolume(start, end);
    if (filtered == null) {
      // Range completo: perfil cumulativo ao vivo, sem subtrair snapshot algum.
      _filteredVolumeLevels = null;
      _volumeFilterActive = false;
    } else {
      _filteredVolumeLevels = filtered;
      _volumeFilterActive = true;
    }

    notifyListeners();
    _syncExtremesPeriod();
  }

  /// Calcula o perfil de volume da janela [start, end] (end inclusivo).
  /// Retorna `null` quando a janela cobre o range completo (perfil cumulativo
  /// ao vivo). Nunca retorna o `_volumeLevels` do período inteiro para uma
  /// janela parcial quando a referência de início não tem snapshot — barras ao
  /// vivo chegam sem `VolumeLevel` — evitando mostrar o volume do dia todo.
  List<VolumeLevel>? _computeWindowVolume(int start, int end) {
    final bars = barsTimeFrameFilter;
    final count = bars.length;
    if (count == 0) return null;

    // Range completo (dia inteiro): mostra o perfil cumulativo ao vivo,
    // sem subtrair snapshot algum (paridade com NewMapFlow.razor).
    if (start <= 0 && end >= count) return null;

    final startIdx = start - 1;
    final endIdx = end.clamp(0, count - 1);
    final endBar = bars[endIdx];

    // Referência de fim: snapshot do último candle da janela; se ausente
    // (barra ao vivo sem VolumeLevel), usa o cumulativo ao vivo.
    final endLevels =
        (endBar.volumeLevel != null && endBar.volumeLevel!.isNotEmpty)
            ? endBar.volumeLevel!
            : _volumeLevels;

    // Referência de início: start==0 → sem barra anterior (primeiro candle
    // incluso); senão snapshot da barra anterior, com fallback para a barra
    // anterior mais próxima que tenha snapshot.
    final List<VolumeLevel> startLevels;
    if (startIdx < 0) {
      startLevels = const [];
    } else {
      final startBar = bars[startIdx];
      startLevels =
          (startBar.volumeLevel != null && startBar.volumeLevel!.isNotEmpty)
              ? startBar.volumeLevel!
              : _findPreviousSnapshot(startIdx - 1);
    }

    if (endLevels.isEmpty) return const <VolumeLevel>[];

    final result = startLevels.isEmpty
        ? List<VolumeLevel>.of(endLevels)
        : VolumeLevelStorageItem.operation(endLevels, startLevels, 'Diff');
    result.sort((a, b) => a.price.compareTo(b.price));
    return result;
  }

  List<VolumeLevel> _findPreviousSnapshot(int fromIndex) {
    final bars = barsTimeFrameFilter;
    for (int i = fromIndex; i >= 0; i--) {
      final b = bars[i];
      if (b.volumeLevel != null && b.volumeLevel!.isNotEmpty) {
        return b.volumeLevel!;
      }
    }
    return const <VolumeLevel>[];
  }

  // --- Structure ---

  Future<void> setMinDistanceStructure(double minDistance) async {
    if (_guardLocked('setMinDistanceStructure')) return;
    final structures =
        await _apiService.setStructureDistance(_symbol, minDistance);
    // Defesa: a resposta pode incluir o 1440 (range diário próprio);
    // o intraday nunca o armazena em `_structures`.
    _structures = structures.where((s) => s.timeFrame != 1440).toList();
    _recomputeStructureChanges(_structures);
    notifyListeners();
  }

  // --- Positions & Orders ---

  void updatePositions(List<PositionInfo> positions) {
    _positions = positions;
    notifyListeners();
  }

  void updateOrders(List<OrderInfo> orders) {
    _orders = orders;
    notifyListeners();
  }

  void updateHistory(List<HistoryDeal> history) {
    _history = history;
    notifyListeners();
  }

  // --- Chart callback ---

  void _onChartUpdate() {
    notifyListeners();
  }

  // --- Dispose ---

  void reset() {
    _processTimer?.cancel();
    _watchdogTimer?.cancel();
    _stopDailyLiveRefresh();
    _stopStrategyPolling();
    _signalRService.dispose();
    _audioService.dispose();
    if (_ownsStrategyService) _strategyService.dispose();
  }

  @override
  void dispose() {
    reset();
    super.dispose();
  }
}

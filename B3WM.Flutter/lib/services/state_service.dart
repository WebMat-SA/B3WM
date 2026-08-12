import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/structure_change_item.dart';
import '../models/symbol_config.dart';
import 'api_service.dart';
import 'signalr_service.dart';
import 'preferences_service.dart';
import 'audio_service.dart';
import '../models/trade_models.dart';
import '../models/signal_event.dart';
import '../models/verifier_config.dart';
import '../models/verifier_state.dart';

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
  })  : _apiService = apiService,
        _signalRService = signalRService,
        _preferencesService = preferencesService,
        _audioService = audioService {
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
  bool get tradingAccountExpanded => _currentConfig.tradingAccountExpanded;
  bool get tradingOrdersExpanded => _currentConfig.tradingOrdersExpanded;
  bool get tradingPositionsExpanded => _currentConfig.tradingPositionsExpanded;
  bool get tradingHistoryExpanded => _currentConfig.tradingHistoryExpanded;

  bool _isStructureUpdating = false;
  bool get isStructureUpdating => _isStructureUpdating;

  // --- Verifier (paper trading) ---
  VerifierState? _verifierState;
  VerifierState? get verifierState => _verifierState;
  bool get verifierRunning => _verifierState?.isRunning ?? false;
  Timer? _verifierTimer;

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
  Future<void> setTimeFrame(int v) async { _currentConfig.timeFrame = v; notifyListeners(); _saveConfigForSymbol(_symbol); await loadData(); }
  void setBubbleVisible(bool v) { _currentConfig.bubbleVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSize(double v) { _currentConfig.bubbleSize = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleOpacity(double v) { _currentConfig.bubbleOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSizeMin(double v) { _currentConfig.bubbleSizeMin = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSizeMax(double v) { _currentConfig.bubbleSizeMax = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setThresholdBubble(int v) { _currentConfig.thresholdBubble = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setProfileVisible(bool v) { _currentConfig.profileVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileSizeH(double v) { _currentConfig.profileSizeH = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileSizeV(double v) { _currentConfig.profileSizeV = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileOpacity(double v) { _currentConfig.profileOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setProfileAutoByPriceStructure(bool v) {
    _currentConfig.profileAutoByPriceStructure = v;
    notifyListeners();
    _saveConfigForSymbol(_symbol);
    if (v) {
      _applyStructureAutoFilter();
    } else {
      _applyVolumeFilter(0, barsTimeFrameFilter.length);
    }
  }

  void setStructureVisible(bool v) { _currentConfig.structureVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureAuxVisible(bool v) { _currentConfig.structureAuxVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureOpacity(double v) { _currentConfig.structureOpacity = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setStructureRangeUpd(double v) { _currentConfig.structureRangeUpd = v; _isStructureUpdating = true; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setColorBuyer(String v) { _currentConfig.colorBuyer = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setColorSeller(String v) { _currentConfig.colorSeller = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  Future<void> confirmStructureRangeUpd() async {
    await setMinDistanceStructure(_currentConfig.structureRangeUpd);
    _isStructureUpdating = false;
    notifyListeners();
  }

  void setBubbleAmountFilter(bool v) { _currentConfig.bubbleAmountFilter = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleAgentsFilter(bool v) { _currentConfig.bubbleAgentsFilter = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSoundEnabled(bool v) { _currentConfig.bubbleSoundEnabled = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setBubbleSoundVolume(double v) { _currentConfig.bubbleSoundVolume = v.clamp(0.0, 1.0); notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setTradingHistoryVisible(bool v) { _currentConfig.tradingHistoryVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setPositionVisible(bool v) { _currentConfig.positionVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setOpenOrdersVisible(bool v) { _currentConfig.openOrdersVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setTradingPanelVisible(bool v) { _currentConfig.tradingPanelVisible = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingAccountExpanded(bool v) { _currentConfig.tradingAccountExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingOrdersExpanded(bool v) { _currentConfig.tradingOrdersExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingPositionsExpanded(bool v) { _currentConfig.tradingPositionsExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }
  void setTradingHistoryExpanded(bool v) { _currentConfig.tradingHistoryExpanded = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

  void setYZoom(double v) { _yZoom = v.clamp(0.3, 5.0); notifyListeners(); }

  void selectAllAgents() {
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
    if (_currentConfig.selectedAgents.contains(agent)) {
      _currentConfig.selectedAgents.remove(agent);
    } else {
      _currentConfig.selectedAgents.add(agent);
    }
    notifyListeners();
    _saveConfigForSymbol(_symbol);
  }

  void setAgentThreshold(int agent, int? threshold) {
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
        return SymbolConfig.fromJson(jsonDecode(json), symbol: symbol);
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

    return SymbolConfig(
      timeFrame: p.getInt('TimeFrame') ?? 2,
      thresholdBubble: p.getInt('ThresholdBubble') ?? 250,
      structureRangeUpd: p.getDouble('StructureRangeUpd') ?? 250,
      structureVisible: p.getBool('StructureVisible') ?? true,
      structureAuxVisible: p.getBool('StructureAuxVisible') ?? true,
      structureOpacity: p.getDouble('StructureOpacity') ?? 0.8,
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
      tradingAccountExpanded: false,
      tradingOrdersExpanded: false,
      tradingPositionsExpanded: false,
      tradingHistoryExpanded: false,
    );
  }
  void _saveConfigForSymbol(String symbol) {
    if (symbol.isEmpty || !_configs.containsKey(symbol)) return;
    _preferencesService.setString(
        'Config_$symbol', jsonEncode(_configs[symbol]!.toJson()));
  }

  // --- Process Loop ---
  Timer? _processTimer;
  Timer? _watchdogTimer;
  DateTime _lastChartUpdate = DateTime.now();
  bool _chartGenInProgress = false;

  static const int maxBubbles = 2000;

  void _init() {
    _signalRService.onCloseBar = _handleCloseBar;
    _signalRService.onNewBubble = _handleNewBubble;
    _signalRService.onVolumeUpdate = _handleVolumeUpdate;
    _signalRService.onCurrentBar = _handleCurrentBar;
    _signalRService.onNewStructure = _handleNewStructure;
    _signalRService.onMissedBars = _handleMissedBars;
    _signalRService.onMissedBubbles = _handleMissedBubbles;
    _signalRService.onSignal = _handleSignal;
  }

  Future<void> setSymbol(String value) async {
    _saveConfigForSymbol(_symbol);
    _symbol = value.toUpperCase();
    _allBubbleAgents.clear();
    notifyListeners();
    await loadData();
  }

  Future<void> loadData() async {
    if (_symbol.isEmpty) return;

    _isLoading = true;
    notifyListeners();

    var effectiveDate = DateTime.now();

    try {
      debugPrint('[loadData] Trying today: ${_formatDate(effectiveDate)}');

      var bars = await _apiService.getBars(_symbol, effectiveDate);
      debugPrint('[loadData] Today bars count: ${bars.length}');
      debugPrint('[loadData] Available timeframes: ${bars.map((b) => b.timeFrame).toSet()}');

      if (bars.isEmpty) {
        effectiveDate = await _apiService.findLastDateWithData(_symbol);
        debugPrint('[loadData] Falling back to date: ${_formatDate(effectiveDate)}');
        bars = await _apiService.getBars(_symbol, effectiveDate);
        debugPrint('[loadData] Fallback bars count: ${bars.length}');
      }

      _bars = bars;
      notifyListeners();

      final filteredCount = barsTimeFrameFilter.length;
      debugPrint('[loadData] _timeFrame=${_currentConfig.timeFrame}, filteredCount=$filteredCount, totalBars=${_bars.length}');

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
      debugPrint('[loadData] Bubbles count: ${bubbles.length}');

      // Load volume
      final volumeData = await _apiService.getVolume(_symbol, effectiveDate);
      if (volumeData != null && volumeData.volumes.isNotEmpty) {
        _volumeLevels = volumeData.volumes;
        debugPrint('[loadData] Volume levels loaded: ${_volumeLevels.length}');
      } else if (_bars.isNotEmpty) {
        final barsWithVol = _bars
            .where((b) => b.volumeLevel != null && b.volumeLevel!.isNotEmpty)
            .toList();
        debugPrint('[loadData] Bars with volumeLevel: ${barsWithVol.length}');
        if (barsWithVol.isNotEmpty) {
          _volumeLevels = barsWithVol.reduce(
            (a, b) => a.date.compareTo(b.date) > 0 ? a : b).volumeLevel ?? [];
          debugPrint('[loadData] Volume from last bar: ${_volumeLevels.length}');
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
      debugPrint('[loadData] Structures count: ${structures.length}');
      if (_currentConfig.profileAutoByPriceStructure) {
        _applyStructureAutoFilter();
      }
      notifyListeners();

    } catch (e) {
      debugPrint('[loadData] Error: $e');
      debugPrint('[loadData] Stack: ${StackTrace.current}');
    } finally {
      _isLoading = false;

      await _signalRService.startConnection(_symbol, _currentConfig.timeFrame);
      await _refreshStructuresAfterConnection(effectiveDate);
      _startProcessLoop();
      _startWatchdog();
      notifyListeners();
    }
  }

  Future<List<StructureStorageItem>> _fetchStructures(
    DateTime date, {
    bool allowRetry = true,
  }) async {
    var result = await _apiService.getStructure(
        _symbol, date, _currentConfig.structureRangeUpd);
    for (var attempt = 0;
        allowRetry && result.isEmpty && attempt < 4;
        attempt++) {
      debugPrint(
          '[loadData] Structures empty on attempt ${attempt + 1}, retrying...');
      await Future.delayed(const Duration(seconds: 2));
      result = await _apiService.getStructure(
          _symbol, date, _currentConfig.structureRangeUpd);
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
        debugPrint('[loadData] Merged ${fetched.length} structures after connection');
      }
    } catch (e) {
      debugPrint('[loadData] Structure refresh after connection error: $e');
    }
  }

  String _formatDate(DateTime d) =>
      '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';


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
        debugPrint('ProcessLoop error: $e');
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
        debugPrint(
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

    notifyListeners();
  }

  void _handleCurrentBar(BarStorageItem bar) {
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
    _volumeLevels = volumes.volumes;

    if (barsTimeFrameFilter.isNotEmpty) {
      _applyVolumeFilter(_dateRangeStart, _dateRangeEnd);
    }

    notifyListeners();
  }

  void _handleNewStructure(StructureStorageItem structure) {
    if (structure.symbol != _symbol || structure.timeFrame != _currentConfig.timeFrame) return;
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
    for (final c in _structureChanges) {
      if (c.isUp != lastChange.isUp && c.isUpMove != lastChange.isUpMove) {
        anchor = c;
        break;
      }
    }
    if (anchor == null) {
      _applyVolumeFilter(0, end);
      return;
    }
    var start = 0;
    for (int i = bars.length - 1; i >= 0; i--) {
      if (!bars[i].date.isAfter(anchor.date)) {
        start = i;
        break;
      }
    }
    start = start.clamp(0, end - 1);
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
    final structures =
        await _apiService.setStructureDistance(_symbol, minDistance);
    _structures = structures;
    _recomputeStructureChanges(structures);
    notifyListeners();
  }

  // --- Verifier (paper trading) ---

  void _handleSignal(SignalEvent signal) {
    final state = _verifierState;
    if (state == null ||
        signal.symbol != state.symbol ||
        signal.timeFrame != state.timeFrame) {
      return;
    }
    state.signals.insert(0, signal);
    if (state.signals.length > 200) {
      state.signals.removeRange(200, state.signals.length);
    }
    notifyListeners();
  }

  Future<int> startVerifier(VerifierConfig config) async {
    final status = await _apiService.startVerifier(config);
    if (status == 200) {
      _verifierState = VerifierState(
        symbol: config.symbol,
        timeFrame: config.timeFrame,
        isRunning: true,
        config: config,
      );
      notifyListeners();
      _startVerifierPolling();
      refreshVerifierState();
    }
    return status;
  }

  Future<int> stopVerifier() async {
    final state = _verifierState;
    if (state == null) return 0;
    final status = await _apiService.stopVerifier(state.symbol, state.timeFrame);
    if (status == 200) {
      state.isRunning = false;
      notifyListeners();
      _stopVerifierPolling();
      refreshVerifierState();
    }
    return status;
  }

  Future<int> resetVerifier() async {
    final state = _verifierState;
    if (state == null) return 0;
    final status = await _apiService.resetVerifier(state.symbol, state.timeFrame);
    if (status == 200) {
      refreshVerifierState();
    }
    return status;
  }

  Future<void> refreshVerifierState() async {
    final state = _verifierState;
    final symbol = state?.symbol ?? _symbol;
    final timeFrame = state?.timeFrame ?? _currentConfig.timeFrame;
    if (symbol.isEmpty) return;
    final fresh = await _apiService.getVerifierState(symbol, timeFrame);
    if (fresh != null) {
      _verifierState = fresh;
      notifyListeners();
    }
  }

  void _startVerifierPolling() {
    _verifierTimer?.cancel();
    _verifierTimer = Timer.periodic(const Duration(seconds: 4), (_) {
      refreshVerifierState();
    });
  }

  void _stopVerifierPolling() {
    _verifierTimer?.cancel();
    _verifierTimer = null;
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
    _stopVerifierPolling();
    _signalRService.dispose();
    _audioService.dispose();
  }

  @override
  void dispose() {
    reset();
    super.dispose();
  }
}

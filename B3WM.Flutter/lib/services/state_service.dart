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

  List<StructureChangeItem> get visibleStructureChanges {
    final bars = barsTimeFrameFilter;
    if (bars.isEmpty) return [];
    final startIdx = _dateRangeStart.clamp(0, bars.length);
    final endIdx = _dateRangeEnd > 0
        ? _dateRangeEnd.clamp(0, bars.length)
        : bars.length;
    if (startIdx >= endIdx) return [];
    final startDate = bars[startIdx].date;
    final endDate = bars[endIdx - 1].date;
    return _structureChanges
        .where((c) => !c.date.isBefore(startDate) && !c.date.isAfter(endDate))
        .toList();
  }

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

  bool _isStructureUpdating = false;
  bool get isStructureUpdating => _isStructureUpdating;

  // Data-driven (not persisted config)
  final Set<int> _allBubbleAgents = {};
  Set<int> get allBubbleAgents => _allBubbleAgents;

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
  void setProfileAutoByPriceStructure(bool v) { _currentConfig.profileAutoByPriceStructure = v; notifyListeners(); _saveConfigForSymbol(_symbol); }

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

  void setYZoom(double v) { _yZoom = v.clamp(0.3, 5.0); notifyListeners(); }

  void selectAllAgents() {
    if (_currentConfig.selectedAgents.length == _allBubbleAgents.length) {
      _currentConfig.selectedAgents = [];
    } else {
      _currentConfig.selectedAgents = _allBubbleAgents.toList();
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
  bool _migrated = false;

  SymbolConfig _loadConfigForSymbol(String symbol) {
    final json = _preferencesService.getString('Config_$symbol');
    if (json != null) {
      try {
        return SymbolConfig.fromJson(jsonDecode(json));
      } catch (_) {}
    }

    if (!_migrated) {
      _migrated = true;
      final old = _tryMigrateFromOldKeys();
      if (old != null) {
        _configs[symbol] = old;
        _saveConfigForSymbol(symbol);
        return old;
      }
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
      bubbleAmountFilter: p.getBool('BubbleAmountFilter') ?? true,
      bubbleAgentsFilter: p.getBool('BubbleAgentsFilter') ?? true,
      bubbleSoundEnabled: p.getBool('BubbleSoundEnabled') ?? true,
      bubbleSoundVolume: p.getDouble('BubbleSoundVolume') ?? 0.5,
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

    try {
      var effectiveDate = DateTime.now();
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
      for (final b in bubbles) {
        _allBubbleAgents.add(b.agent);
      }
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
        _filteredVolumeLevels = List.from(_volumeLevels);
        _volumeFilterActive = true;
      }
      notifyListeners();

      // Load structure
      final structures = await _apiService.getStructure(
          _symbol, effectiveDate, _currentConfig.structureRangeUpd);
      _structures = structures;
      _recomputeStructureChanges(structures);
      debugPrint('[loadData] Structures count: ${structures.length}');
      notifyListeners();

    } catch (e) {
      debugPrint('[loadData] Error: $e');
      debugPrint('[loadData] Stack: ${StackTrace.current}');
    } finally {
      _isLoading = false;

      await _signalRService.startConnection(_symbol, _currentConfig.timeFrame);
      _startProcessLoop();
      _startWatchdog();
      notifyListeners();
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
      _bars[existingIndex] = bar;
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
    _currentBar = bar;

    final existingIndex =
        _bars.indexWhere((b) => b.date == bar.date && b.timeFrame == bar.timeFrame);
    if (existingIndex >= 0) {
      _bars[existingIndex] = bar;
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

  void _handleNewBubble(BubbleStorageItem data) {
    if (_allBubbleAgents.add(data.agent)) {
      _currentConfig.selectedAgents.add(data.agent);
    }
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

    final count = barsTimeFrameFilter.length;
    if (count > 0) {
      final startIdx = (_dateRangeStart - 1).clamp(0, count - 1);
      final endIdx = _dateRangeEnd.clamp(0, count - 1);
      final startBar = barsTimeFrameFilter.elementAtOrNull(startIdx);
      final endBar = barsTimeFrameFilter.elementAtOrNull(endIdx);
      if (startBar?.volumeLevel != null &&
          startBar!.volumeLevel!.isNotEmpty &&
          endBar?.volumeLevel != null &&
          endBar!.volumeLevel!.isNotEmpty) {
        _filteredVolumeLevels = VolumeLevelStorageItem.operation(
            endBar.volumeLevel!, startBar.volumeLevel!, 'Diff');
        _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
        _volumeFilterActive = true;
      } else if (startBar?.volumeLevel != null &&
          startBar!.volumeLevel!.isNotEmpty) {
        _filteredVolumeLevels = VolumeLevelStorageItem.operation(
            _volumeLevels, startBar.volumeLevel!, 'Diff');
        _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
        _volumeFilterActive = true;
      } else {
        _filteredVolumeLevels = List.from(_volumeLevels);
        _volumeFilterActive = true;
      }
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
      _applyVolumeFilter(0, barsTimeFrameFilter.length);
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

  void _applyVolumeFilter(int start, int end) {
    _dateRangeStart = start;
    _dateRangeEnd = end;

    final count = barsTimeFrameFilter.length;
    if (count == 0) return;

    final bars = barsTimeFrameFilter;
    final startIdx = (start - 1).clamp(0, count - 1);
    final endIdx = end.clamp(0, count - 1);
    var startBar = bars.elementAtOrNull(startIdx);
    var endBar = bars.elementAtOrNull(endIdx);

    if (startBar != null && endBar != null) {
      final startLevels = startBar.volumeLevel;
      final endLevels = endBar.volumeLevel;

      if (endLevels != null && endLevels.isNotEmpty && startLevels != null && startLevels.isNotEmpty) {
        _filteredVolumeLevels = VolumeLevelStorageItem.operation(
            endLevels, startLevels, 'Diff');
        _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
        _volumeFilterActive = true;
      } else {
        BarStorageItem? foundStart = (startLevels != null && startLevels.isNotEmpty) ? startBar : null;
        BarStorageItem? foundEnd = (endLevels != null && endLevels.isNotEmpty) ? endBar : null;

        if (foundStart == null) {
          for (int i = startIdx - 1; i >= 0; i--) {
            final b = bars[i];
            if (b.volumeLevel != null && b.volumeLevel!.isNotEmpty) {
              foundStart = b;
              break;
            }
          }
        }

        if (foundEnd == null && foundStart != null) {
          for (int i = endIdx + 1; i < bars.length; i++) {
            final b = bars[i];
            if (b.volumeLevel != null && b.volumeLevel!.isNotEmpty) {
              foundEnd = b;
              break;
            }
          }
        }

        if (foundStart != null && foundEnd != null) {
          _filteredVolumeLevels = VolumeLevelStorageItem.operation(
              foundEnd.volumeLevel!, foundStart.volumeLevel!, 'Diff');
          _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
          _volumeFilterActive = true;
        } else if (foundStart != null) {
          _filteredVolumeLevels = VolumeLevelStorageItem.operation(
              _volumeLevels, foundStart.volumeLevel!, 'Diff');
          _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
          _volumeFilterActive = true;
        }
      }
    }

    if (_filteredVolumeLevels == null && _volumeLevels.isNotEmpty) {
      _filteredVolumeLevels = List.from(_volumeLevels);
      _volumeFilterActive = true;
    }

    notifyListeners();
  }

  // --- Structure ---

  Future<void> setMinDistanceStructure(double minDistance) async {
    final structures =
        await _apiService.setStructureDistance(_symbol, minDistance);
    _structures = structures;
    _recomputeStructureChanges(structures);
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
    _signalRService.dispose();
    _audioService.dispose();
  }

  @override
  void dispose() {
    reset();
    super.dispose();
  }
}

extension _ListExtension<T> on List<T> {
  T? elementAtOrNull(int index) {
    if (index < 0 || index >= length) return null;
    return this[index];
  }
}

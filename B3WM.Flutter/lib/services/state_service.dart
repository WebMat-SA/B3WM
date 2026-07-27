import 'dart:async';
import 'package:flutter/foundation.dart';
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import 'api_service.dart';
import 'signalr_service.dart';
import 'preferences_service.dart';

class StateService extends ChangeNotifier {
  final ApiService _apiService;
  final SignalRService _signalRService;
  // ignore: unused_field
  final PreferencesService _preferencesService;

  StateService({
    required ApiService apiService,
    required SignalRService signalRService,
    required PreferencesService preferencesService,
  })  : _apiService = apiService,
        _signalRService = signalRService,
        _preferencesService = preferencesService {
    _init();
  }

  // --- State ---
  String _symbol = '';
  String get symbol => _symbol;

  int _timeFrame = 2;
  int get timeFrame => _timeFrame;

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  int _throttlingDelayMs = 500;
  int get throttlingDelayMs => _throttlingDelayMs;

  List<BarStorageItem> _bars = [];
  List<BarStorageItem> get bars => _bars;

  List<BarStorageItem> get barsTimeFrameFilter =>
      _bars.where((b) => b.timeFrame == _timeFrame).toList()
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
          .where((s) => s.timeFrame == _timeFrame && s.symbol == _symbol)
          .toList();

  // Bubble drawer filters
  bool _bubbleAmountFilter = true;
  bool get bubbleAmountFilter => _bubbleAmountFilter;
  bool _bubbleAgentsFilter = true;
  bool get bubbleAgentsFilter => _bubbleAgentsFilter;
  bool _bubbleSoundEnabled = true;
  bool get bubbleSoundEnabled => _bubbleSoundEnabled;

  void setBubbleAmountFilter(bool v) { _bubbleAmountFilter = v; notifyListeners(); _savePreferences(); }
  void setBubbleAgentsFilter(bool v) { _bubbleAgentsFilter = v; notifyListeners(); _savePreferences(); }
  void setBubbleSoundEnabled(bool v) { _bubbleSoundEnabled = v; notifyListeners(); _savePreferences(); }

  // Range
  int _dateRangeStart = 0;
  int _dateRangeEnd = 0;
  int get dateRangeStart => _dateRangeStart;
  int get dateRangeEnd => _dateRangeEnd;
  int get barsCount => barsTimeFrameFilter.length;

  // Config
  double _bubbleSize = 1.0;
  double get bubbleSize => _bubbleSize;
  double _bubbleOpacity = 0.7;
  double get bubbleOpacity => _bubbleOpacity;
  bool _bubbleVisible = true;
  bool get bubbleVisible => _bubbleVisible;
  double _bubbleSizeMin = 20;
  double get bubbleSizeMin => _bubbleSizeMin;
  double _bubbleSizeMax = 100;
  double get bubbleSizeMax => _bubbleSizeMax;

  double _profileSizeH = 1.0;
  double get profileSizeH => _profileSizeH;
  double _profileSizeV = 1.0;
  double get profileSizeV => _profileSizeV;
  double _profileOpacity = 0.5;
  double get profileOpacity => _profileOpacity;
  bool _profileVisible = true;
  bool get profileVisible => _profileVisible;
  bool _profileAutoByPriceStructure = false;
  bool get profileAutoByPriceStructure => _profileAutoByPriceStructure;

  bool _structureVisible = true;
  bool get structureVisible => _structureVisible;
  bool _structureAuxVisible = true;
  bool get structureAuxVisible => _structureAuxVisible;
  double _structureOpacity = 0.8;
  double get structureOpacity => _structureOpacity;

  double _structureRangeUpd = 250;
  double get structureRangeUpd => _structureRangeUpd;

  String _colorBuyer = '#4488ff';
  String get colorBuyer => _colorBuyer;
  String _colorSeller = '#ff4444';
  String get colorSeller => _colorSeller;

  List<int> _selectedAgents = [];
  List<int> get selectedAgents => _selectedAgents;
  final Set<int> _allBubbleAgents = {};
  Set<int> get allBubbleAgents => _allBubbleAgents;
  final Map<int, int> _agentThresholds = {};
  int _thresholdBubble = 250;
  int get thresholdBubble => _thresholdBubble;

  double _yZoom = 1.0;
  double get yZoom => _yZoom;

  bool get isConnected => _signalRService.isConnected;

  // Config setters
  void setTimeFrame(int v) { _timeFrame = v; notifyListeners(); _savePreferences(); }
  void setBubbleVisible(bool v) { _bubbleVisible = v; notifyListeners(); _savePreferences(); }
  void setBubbleSize(double v) { _bubbleSize = v; notifyListeners(); _savePreferences(); }
  void setBubbleOpacity(double v) { _bubbleOpacity = v; notifyListeners(); _savePreferences(); }
  void setBubbleSizeMin(double v) { _bubbleSizeMin = v; notifyListeners(); _savePreferences(); }
  void setBubbleSizeMax(double v) { _bubbleSizeMax = v; notifyListeners(); _savePreferences(); }
  void setThresholdBubble(int v) { _thresholdBubble = v; notifyListeners(); _savePreferences(); }

  void setProfileVisible(bool v) { _profileVisible = v; notifyListeners(); _savePreferences(); }
  void setProfileSizeH(double v) { _profileSizeH = v; notifyListeners(); _savePreferences(); }
  void setProfileSizeV(double v) { _profileSizeV = v; notifyListeners(); _savePreferences(); }
  void setProfileOpacity(double v) { _profileOpacity = v; notifyListeners(); _savePreferences(); }

  void setStructureVisible(bool v) { _structureVisible = v; notifyListeners(); _savePreferences(); }
  void setStructureAuxVisible(bool v) { _structureAuxVisible = v; notifyListeners(); _savePreferences(); }
  void setStructureOpacity(double v) { _structureOpacity = v; notifyListeners(); _savePreferences(); }
  void setStructureRangeUpd(double v) { _structureRangeUpd = v; notifyListeners(); _savePreferences(); }

  void setColorBuyer(String v) { _colorBuyer = v; notifyListeners(); _savePreferences(); }
  void setColorSeller(String v) { _colorSeller = v; notifyListeners(); _savePreferences(); }

  void setProfileAutoByPriceStructure(bool v) { _profileAutoByPriceStructure = v; notifyListeners(); _savePreferences(); }

  void setYZoom(double v) { _yZoom = v.clamp(0.3, 5.0); notifyListeners(); }

  void selectAllAgents() {
    if (_selectedAgents.length == _allBubbleAgents.length) {
      _selectedAgents = [];
    } else {
      _selectedAgents = _allBubbleAgents.toList();
    }
    notifyListeners();
    _savePreferences();
  }

  void toggleAgent(int agent) {
    if (_selectedAgents.contains(agent)) {
      _selectedAgents.remove(agent);
    } else {
      _selectedAgents.add(agent);
    }
    notifyListeners();
    _savePreferences();
  }

  void setAgentThreshold(int agent, int? threshold) {
    if (threshold != null) {
      _agentThresholds[agent] = threshold;
    } else {
      _agentThresholds.remove(agent);
    }
    notifyListeners();
    _savePreferences();
  }

  // --- Process Loop ---
  Timer? _processTimer;
  Timer? _watchdogTimer;
  DateTime _lastChartUpdate = DateTime.now();
  bool _chartGenInProgress = false;

  // Constants
  static const int maxBubbles = 2000;

  void _init() {
    _loadPreferences();
    _signalRService.onCloseBar = _handleCloseBar;
    _signalRService.onNewBubble = _handleNewBubble;
    _signalRService.onVolumeUpdate = _handleVolumeUpdate;
    _signalRService.onCurrentBar = _handleCurrentBar;
    _signalRService.onNewStructure = _handleNewStructure;
    _signalRService.onMissedBars = _handleMissedBars;
    _signalRService.onMissedBubbles = _handleMissedBubbles;
  }

  void _loadPreferences() {
    final p = _preferencesService;
    _timeFrame = p.getInt('TimeFrame') ?? _timeFrame;
    _thresholdBubble = p.getInt('ThresholdBubble') ?? _thresholdBubble;
    _structureRangeUpd = p.getDouble('StructureRangeUpd') ?? _structureRangeUpd;
    _structureVisible = p.getBool('StructureVisible') ?? _structureVisible;
    _structureAuxVisible = p.getBool('StructureAuxVisible') ?? _structureAuxVisible;
    _structureOpacity = p.getDouble('StructureOpacity') ?? _structureOpacity;
    _bubbleSize = p.getDouble('BubbleSize') ?? _bubbleSize;
    _bubbleOpacity = p.getDouble('BubbleOpacity') ?? _bubbleOpacity;
    _bubbleVisible = p.getBool('BubbleVisible') ?? _bubbleVisible;
    _bubbleSizeMin = p.getDouble('BubbleSizeMin') ?? _bubbleSizeMin;
    _bubbleSizeMax = p.getDouble('BubbleSizeMax') ?? _bubbleSizeMax;
    _profileSizeH = p.getDouble('ProfileSizeH') ?? _profileSizeH;
    _profileSizeV = p.getDouble('ProfileSizeV') ?? _profileSizeV;
    _profileOpacity = p.getDouble('ProfileOpacity') ?? _profileOpacity;
    _profileVisible = p.getBool('ProfileVisible') ?? _profileVisible;
    _profileAutoByPriceStructure = p.getBool('ProfileAutoByPriceStructure') ?? _profileAutoByPriceStructure;
    _colorBuyer = p.getString('ColorBuyer') ?? _colorBuyer;
    _colorSeller = p.getString('ColorSeller') ?? _colorSeller;
    final selected = p.getIntList('SelectedAgents');
    if (selected != null) _selectedAgents = selected;
    final all = p.getIntList('_allBubbleAgents');
    if (all != null) { _allBubbleAgents.clear(); _allBubbleAgents.addAll(all); }
    final thresholds = p.getIntIntMap('AgentThresholds');
    if (thresholds != null) { _agentThresholds.clear(); _agentThresholds.addAll(thresholds); }
    _bubbleAmountFilter = p.getBool('BubbleAmountFilter') ?? _bubbleAmountFilter;
    _bubbleAgentsFilter = p.getBool('BubbleAgentsFilter') ?? _bubbleAgentsFilter;
    _bubbleSoundEnabled = p.getBool('BubbleSoundEnabled') ?? _bubbleSoundEnabled;
  }

  void _savePreferences() {
    final p = _preferencesService;
    p.setInt('TimeFrame', _timeFrame);
    p.setInt('ThresholdBubble', _thresholdBubble);
    p.setDouble('StructureRangeUpd', _structureRangeUpd);
    p.setBool('StructureVisible', _structureVisible);
    p.setBool('StructureAuxVisible', _structureAuxVisible);
    p.setDouble('StructureOpacity', _structureOpacity);
    p.setDouble('BubbleSize', _bubbleSize);
    p.setDouble('BubbleOpacity', _bubbleOpacity);
    p.setBool('BubbleVisible', _bubbleVisible);
    p.setDouble('BubbleSizeMin', _bubbleSizeMin);
    p.setDouble('BubbleSizeMax', _bubbleSizeMax);
    p.setDouble('ProfileSizeH', _profileSizeH);
    p.setDouble('ProfileSizeV', _profileSizeV);
    p.setDouble('ProfileOpacity', _profileOpacity);
    p.setBool('ProfileVisible', _profileVisible);
    p.setBool('ProfileAutoByPriceStructure', _profileAutoByPriceStructure);
    p.setString('ColorBuyer', _colorBuyer);
    p.setString('ColorSeller', _colorSeller);
    p.setIntList('SelectedAgents', _selectedAgents);
    p.setIntList('_allBubbleAgents', _allBubbleAgents.toList());
    p.setIntIntMap('AgentThresholds', _agentThresholds);
    p.setBool('BubbleAmountFilter', _bubbleAmountFilter);
    p.setBool('BubbleAgentsFilter', _bubbleAgentsFilter);
    p.setBool('BubbleSoundEnabled', _bubbleSoundEnabled);
  }

  Future<void> setSymbol(String value) async {
    _symbol = value.toUpperCase();
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
      debugPrint('[loadData] _timeFrame=$_timeFrame, filteredCount=$filteredCount, totalBars=${_bars.length}');

      _dateRangeStart = 0;
      _dateRangeEnd = filteredCount;
      _volumeFilterActive = false;
      _filteredVolumeLevels = null;

      // Load bubbles
      final bubbles = await _apiService.getBubbles(_symbol, effectiveDate);
      _bubbles = bubbles;
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
      notifyListeners();

      // Load structure
      final structures = await _apiService.getStructure(
          _symbol, effectiveDate, _structureRangeUpd);
      _structures = structures;
      debugPrint('[loadData] Structures count: ${structures.length}');
      notifyListeners();

    } catch (e) {
      debugPrint('[loadData] Error: $e');
      debugPrint('[loadData] Stack: ${StackTrace.current}');
    } finally {
      _isLoading = false;

      await _signalRService.startConnection(_symbol, _timeFrame);
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
          await _signalRService.startConnection(_symbol, _timeFrame);
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
      _selectedAgents.add(data.agent);
    }
    _bubbles.add(data);
    if (_bubbles.length > maxBubbles) {
      _bubbles.removeRange(0, _bubbles.length - maxBubbles);
    }
    notifyListeners();
  }

  void _handleVolumeUpdate(VolumeLevelStorageItem volumes) {
    _volumeLevels = volumes.volumes;

    final count = barsTimeFrameFilter.length;
    if (count > 0) {
      final startIdx = (_dateRangeStart - 1).clamp(0, count - 1);
      final startBar = barsTimeFrameFilter.elementAtOrNull(startIdx);
      if (startBar?.volumeLevel != null && startBar!.volumeLevel!.isNotEmpty) {
        _filteredVolumeLevels = VolumeLevelStorageItem.operation(
            volumes.volumes, startBar.volumeLevel!, 'Diff');
        _filteredVolumeLevels!.sort((a, b) => a.price.compareTo(b.price));
        _volumeFilterActive = true;
      }
    }

    notifyListeners();
  }

  void _handleNewStructure(StructureStorageItem structure) {
    if (structure.symbol != _symbol) return;
    _structures.add(structure);
    if (_profileAutoByPriceStructure) {
      _applyVolumeFilter(0, barsTimeFrameFilter.length);
    }
    notifyListeners();
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

    notifyListeners();
  }

  // --- Structure ---

  Future<void> setMinDistanceStructure(double minDistance) async {
    final structures =
        await _apiService.setStructureDistance(_symbol, minDistance);
    _structures = structures;
    notifyListeners();
  }

  // --- Chart callback ---

  void _onChartUpdate() {
    // Called when chart needs to regenerate.
    // The Flutter CustomPainter will read state directly from this service.
    // Notify listeners to trigger repaint.
    notifyListeners();
  }

  Map<int, int> get agentThresholds => Map.unmodifiable(_agentThresholds);

  int getThreshold(int? agent) {
    if (agent == null) return 0;
    return _agentThresholds[agent] ?? _thresholdBubble;
  }

  // --- Dispose ---

  void reset() {
    _processTimer?.cancel();
    _watchdogTimer?.cancel();
    _signalRService.dispose();
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

import 'dart:async';
import 'package:signalr_core/signalr_core.dart' as signalr;
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/signal_event.dart';
import '../models/throttling_data.dart';
import '../models/extreme_storage_item.dart';
import '../models/vwap_history_point.dart';
import 'api_service.dart';
import '../app_log.dart';

class SignalRService {
  signalr.HubConnection? _hubConnection;
  final String _hubUrl;
  final ApiService _apiService;

  String? _symbol;
  int? _timeFrame;
  DateTime? _lastBarTime;
  DateTime? _lastBubbleTime;
  bool _started = false;

  bool get isConnected =>
      _hubConnection?.state == signalr.HubConnectionState.connected;
  bool get started => _started;

  void Function(BarStorageItem)? onCloseBar;
  void Function(BubbleStorageItem)? onNewBubble;
  void Function(VolumeLevelStorageItem)? onVolumeUpdate;
  void Function(BarStorageItem)? onCurrentBar;
  /// Snapshot ao vivo da barra diária (TF 1440), que chega no mesmo payload
  /// do throttling — espelha o caminho da barra corrente intraday.
  void Function(BarStorageItem)? onDailyBar;
  void Function(StructureStorageItem)? onNewStructure;
  void Function(SignalEvent)? onSignal;
  void Function(ExtremeStorageItem)? onExtreme;
  void Function(List<BarStorageItem>)? onMissedBars;
  void Function(List<BubbleStorageItem>)? onMissedBubbles;
  void Function(VwapHistoryPoint)? onVwapUpdate;
  void Function(List<VwapHistoryPoint>)? onVwapHistory;

  SignalRService({required String hubUrl, required ApiService apiService})
      : _hubUrl = hubUrl,
        _apiService = apiService;

  Future<void> startConnection(
      String symbol, int? timeFrame) async {
    _symbol = symbol;
    _timeFrame = timeFrame;

    if (_hubConnection != null) {
      await stopConnection();
    }

    _started = true;

    try {
      _hubConnection = signalr.HubConnectionBuilder()
          .withUrl(_hubUrl)
          .withAutomaticReconnect()
          .build();

      _hubConnection!.onreconnecting((Exception? error) {
      });

      _hubConnection!.onreconnected((String? connectionId) async {
        if (_symbol != null) {
          await _hubConnection!.invoke('JoinGroup', args: [_symbol!]);
        }
        if (_timeFrame != null && _lastBarTime != null) {
          await _tryFetchMissedBars();
        }
        if (_lastBubbleTime != null) {
          await _tryFetchMissedBubbles();
        }
      });

      _hubConnection!.onclose((Exception? error) async {
        await Future.delayed(const Duration(seconds: 3));
        if (_started && _symbol != null) {
          await startConnection(_symbol!, _timeFrame);
        }
      });

      _hubConnection!.on('ReceiveOnCloseBar', (List<dynamic>? args) => _handleCloseBar(args));
      _hubConnection!.on('ReceiveOnBubble', (List<dynamic>? args) => _handleBubble(args));
      _hubConnection!.on('ReceiveOnStructure', (List<dynamic>? args) => _handleStructure(args));
      _hubConnection!.on('ReceiveThrottlingData', (List<dynamic>? args) => _handleThrottlingData(args));
      _hubConnection!.on('ReceiveOnSignal', (List<dynamic>? args) => _handleSignal(args));
      _hubConnection!.on('ReceiveOnExtreme', (List<dynamic>? args) => _handleExtreme(args));
      _hubConnection!.on('ReceiveVwapUpdate', (List<dynamic>? args) => _handleVwapUpdate(args));
      _hubConnection!.on('ReceiveVwapHistory', (List<dynamic>? args) => _handleVwapHistory(args));

      await _hubConnection!.start();

      if (_symbol != null) {
        await _hubConnection!.invoke('JoinGroup', args: [_symbol!]);
      }

    } catch (e) {
    }
  }

  Future<void> stopConnection() async {
    _started = false;
    try {
      await _hubConnection?.stop();
    } catch (_) {}
    _hubConnection = null;
  }

  void _handleCloseBar(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final bar = BarStorageItem.fromJson(json);
    _lastBarTime = bar.date;
    onCloseBar?.call(bar);
  }

  void _handleBubble(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final bubble = BubbleStorageItem.fromJson(json);
    _lastBubbleTime = bubble.date;
    onNewBubble?.call(bubble);
  }

  void _handleStructure(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final structure = StructureStorageItem.fromJson(json);
    onNewStructure?.call(structure);
  }

  void _handleThrottlingData(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final data = ThrottlingData.fromJson(json);

    BarStorageItem? currentBar;
    VolumeLevelStorageItem? currentVolume;

    if (_timeFrame != null) {
      currentBar = data.candle
          .where((c) => c.timeFrame == _timeFrame)
          .firstOrNull;
    }
    // A barra 1440 vinha no payload mas era descartada pelo filtro acima —
    // por isso o candle diário nunca atualizava como o 2min.
    final dailyBar =
        data.candle.where((c) => c.timeFrame == 1440).firstOrNull;
    currentVolume = data.volume;

    if (currentBar != null) {
      _lastBarTime = currentBar.date;
      onCurrentBar?.call(currentBar);
    }
    if (dailyBar != null) {
      onDailyBar?.call(dailyBar);
    }
    if (currentVolume != null) {
      onVolumeUpdate?.call(currentVolume);
    }
    // VWAP fonte única do servidor (throttling 4x/s): atualiza ponto do minuto.
    if (data.vwap != null) {
      onVwapUpdate?.call(data.vwap!);
    }
  }

  void _handleVwapUpdate(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    try {
      final json = args[0] as Map<String, dynamic>;
      final point = VwapHistoryPoint.fromJson(json);
      onVwapUpdate?.call(point);
    } catch (e) {
      logD('Failed to parse VwapUpdate: $e');
    }
  }

  void _handleVwapHistory(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    try {
      final list = (args[0] as List)
          .map((e) => VwapHistoryPoint.fromJson(e as Map<String, dynamic>))
          .toList();
      onVwapHistory?.call(list);
    } catch (e) {
      logD('Failed to parse VwapHistory: $e');
    }
  }

  void _handleSignal(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final signal = SignalEvent.fromJson(json);
    onSignal?.call(signal);
  }

  void _handleExtreme(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final extreme = ExtremeStorageItem.fromJson(json);
    onExtreme?.call(extreme);
  }

  Future<void> _tryFetchMissedBars() async {
    if (_symbol == null || _timeFrame == null || _lastBarTime == null) return;
    try {
      final missed =
          await _apiService.getLiveBarsSince(_symbol!, _timeFrame!, _lastBarTime!);
      if (missed.isNotEmpty) {
        onMissedBars?.call(missed);
      }
    } catch (e) {
      logD('Failed to fetch missed bars: $e');
    }
  }

  Future<void> _tryFetchMissedBubbles() async {
    if (_symbol == null || _lastBubbleTime == null) return;
    try {
      final missed =
          await _apiService.getLiveBubblesSince(_symbol!, _lastBubbleTime!);
      if (missed.isNotEmpty) {
        onMissedBubbles?.call(missed);
      }
    } catch (e) {
      logD('Failed to fetch missed bubbles: $e');
    }
  }

  Future<void> dispose() async {
    await stopConnection();
  }
}

import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:signalr_core/signalr_core.dart' as signalr;
import '../models/bar_storage_item.dart';
import '../models/bubble_storage_item.dart';
import '../models/volume_level_storage_item.dart';
import '../models/structure_storage_item.dart';
import '../models/indicator_value.dart';
import '../models/throttling_data.dart';
import 'api_service.dart';

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
  void Function(StructureStorageItem)? onNewStructure;
  void Function(IndicatorValue)? onIndicatorValue;
  void Function(List<BarStorageItem>)? onMissedBars;
  void Function(List<BubbleStorageItem>)? onMissedBubbles;

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
      _hubConnection!.on('ReceiveOnIndicatorValue', (List<dynamic>? args) => _handleIndicatorValue(args));

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
    currentVolume = data.volume;

    if (currentBar != null) {
      _lastBarTime = currentBar.date;
      onCurrentBar?.call(currentBar);
    }
    if (currentVolume != null) {
      onVolumeUpdate?.call(currentVolume);
    }
  }

  void _handleIndicatorValue(List<dynamic>? args) {
    if (args == null || args.isEmpty) return;
    final json = args[0] as Map<String, dynamic>;
    final indicator = IndicatorValue.fromJson(json);
    onIndicatorValue?.call(indicator);
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
      debugPrint('Failed to fetch missed bars: $e');
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
      debugPrint('Failed to fetch missed bubbles: $e');
    }
  }

  Future<void> dispose() async {
    await stopConnection();
  }
}

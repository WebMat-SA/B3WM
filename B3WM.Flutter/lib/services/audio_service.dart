import 'package:flutter/foundation.dart';
import 'package:audioplayers/audioplayers.dart';

class AudioService {
  AudioPlayer? _player;

  Future<void> playNotification({double volume = 0.5}) async {
    try {
      _player ??= AudioPlayer();
      await _player?.setVolume(volume);
      await _player?.stop();
      await _player?.play(AssetSource('sounds/icq-message-sound.mp3'));
    } catch (e) {
      debugPrint('[AudioService] Error playing sound: $e');
    }
  }

  void dispose() {
    _player?.dispose();
    _player = null;
  }
}

import 'package:flutter/foundation.dart';

/// Log de diagnóstico central: só emite em debug; silencioso em
/// release/profile (debugPrint puro vaza para o console em produção,
/// incluindo corpos HTTP e paths locais).
void logD(Object? message) {
  if (kDebugMode) {
    debugPrint('$message');
  }
}

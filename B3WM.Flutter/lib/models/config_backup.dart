import 'dart:convert';

import 'symbol_config.dart';

/// Backup versionado de todas as configs por símbolo.
///
/// Formato do arquivo JSON (V1):
/// ```json
/// {
///   "format": "b3wm-config",
///   "version": 1,
///   "app": "b3wm_flutter",
///   "exportedAt": "2026-09-21T12:00:00.000Z",
///   "activeSymbol": "WINFUT",
///   "symbols": { "WINFUT": { ...SymbolConfig... }, "WDOFUT": { ... } }
/// }
/// ```
///
/// O payload de cada símbolo é exatamente o `SymbolConfig.toJson()`
/// (incluindo o bloco aninhado `daily:{}`). A validação é estrita e
/// atômica: qualquer erro lança [FormatException] antes de qualquer
/// mutação no chamador (import nunca aplica pela metade).
class ConfigBackup {
  static const String formatId = 'b3wm-config';
  static const int currentVersion = 1;
  static const String appId = 'b3wm_flutter';

  final int version;
  final DateTime exportedAt;
  final String activeSymbol;
  final Map<String, SymbolConfig> symbols;

  ConfigBackup({
    this.version = currentVersion,
    DateTime? exportedAt,
    this.activeSymbol = '',
    Map<String, SymbolConfig>? symbols,
  })  : exportedAt = exportedAt ?? DateTime.now().toUtc(),
        symbols = symbols ?? const {};

  /// Nomes dos símbolos em ordem alfabética.
  List<String> get symbolNames => symbols.keys.toList()..sort();

  factory ConfigBackup.fromJson(Map<String, dynamic> json) {
    final format = json['format'] as String?;
    if (format != formatId) {
      throw const FormatException(
          'Arquivo inválido: campo "format" esperado "b3wm-config".');
    }
    final version = json['version'];
    if (version != currentVersion) {
      throw FormatException(
          'Versão não suportada: "$version" (suportada: $currentVersion).');
    }
    final rawSymbols = json['symbols'];
    if (rawSymbols is! Map) {
      throw const FormatException(
          'Arquivo inválido: campo "symbols" ausente ou inválido.');
    }
    if (rawSymbols.isEmpty) {
      throw const FormatException(
          'Arquivo inválido: nenhum símbolo em "symbols".');
    }
    final symbols = <String, SymbolConfig>{};
    for (final entry in rawSymbols.entries) {
      final key = entry.key.toString().trim().toUpperCase();
      if (key.isEmpty) {
        throw const FormatException(
            'Arquivo inválido: nome de símbolo vazio.');
      }
      final value = entry.value;
      if (value is! Map<String, dynamic>) {
        throw FormatException(
            'Arquivo inválido: config do símbolo "$key" malformada.');
      }
      try {
        symbols[key] = SymbolConfig.fromJson(value, symbol: key);
      } catch (e) {
        throw FormatException(
            'Arquivo inválido: config do símbolo "$key" malformada ($e).');
      }
    }

    DateTime exportedAt;
    try {
      final raw = json['exportedAt'] as String?;
      exportedAt = raw == null ? DateTime.now().toUtc() : DateTime.parse(raw);
    } catch (_) {
      exportedAt = DateTime.now().toUtc();
    }

    final activeSymbol =
        (json['activeSymbol'] as String? ?? '').trim().toUpperCase();

    return ConfigBackup(
      version: currentVersion,
      exportedAt: exportedAt,
      activeSymbol: activeSymbol,
      symbols: symbols,
    );
  }

  Map<String, dynamic> toJson() => {
        'format': formatId,
        'version': version,
        'app': appId,
        'exportedAt': exportedAt.toUtc().toIso8601String(),
        'activeSymbol': activeSymbol,
        'symbols': symbols.map((k, v) => MapEntry(k, v.toJson())),
      };

  /// JSON pronto para gravar em arquivo (indentado para inspeção manual).
  String toPrettyJson() =>
      const JsonEncoder.withIndent('  ').convert(toJson());

  /// Nome de arquivo sugerido: `b3wm_config_YYYYMMDD_HHmmss.json`.
  static String suggestedFileName([DateTime? now]) {
    final dt = (now ?? DateTime.now()).toLocal();
    String p(int v, [int w = 2]) => v.toString().padLeft(w, '0');
    return 'b3wm_config_${p(dt.year, 4)}${p(dt.month)}${p(dt.day)}_'
        '${p(dt.hour)}${p(dt.minute)}${p(dt.second)}.json';
  }
}

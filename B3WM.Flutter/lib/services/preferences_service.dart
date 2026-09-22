import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

class PreferencesService {
  static const _prefix = 'b3wm_';
  SharedPreferences? _prefs;

  Future<void> init() async {
    _prefs = await SharedPreferences.getInstance();
  }

  String? getString(String key) => _prefs?.getString('$_prefix$key');
  Future<bool> setString(String key, String value) =>
      _prefs?.setString('$_prefix$key', value) ?? Future.value(false);

  int? getInt(String key) => _prefs?.getInt('$_prefix$key');
  Future<bool> setInt(String key, int value) =>
      _prefs?.setInt('$_prefix$key', value) ?? Future.value(false);

  double? getDouble(String key) => _prefs?.getDouble('$_prefix$key');
  Future<bool> setDouble(String key, double value) =>
      _prefs?.setDouble('$_prefix$key', value) ?? Future.value(false);

  bool? getBool(String key) => _prefs?.getBool('$_prefix$key');
  Future<bool> setBool(String key, bool value) =>
      _prefs?.setBool('$_prefix$key', value) ?? Future.value(false);

  List<String>? getStringList(String key) =>
      _prefs?.getStringList('$_prefix$key');

  Future<bool> setStringList(String key, List<String> value) =>
      _prefs?.setStringList('$_prefix$key', value) ?? Future.value(false);

  T? getObject<T>(String key, T Function(Map<String, dynamic>) fromJson) {
    final str = getString(key);
    if (str == null) return null;
    try {
      return fromJson(jsonDecode(str) as Map<String, dynamic>);
    } catch (_) {
      return null;
    }
  }

  Future<bool> setObject(String key, Map<String, dynamic> toJson) =>
      setString(key, jsonEncode(toJson));

  Future<bool> remove(String key) =>
      _prefs?.remove('$_prefix$key') ?? Future.value(false);

  /// Todas as chaves brutas do SharedPreferences (com prefixo `b3wm_`).
  Set<String> getKeys() => _prefs?.getKeys() ?? {};

  /// Símbolos com config persistida (`Config_$symbol`).
  /// Ex.: armazena `b3wm_Config_WINFUT` -> retorna `WINFUT`.
  List<String> getConfigSymbols() {
    const fullPrefix = '${_prefix}Config_';
    final out = <String>[];
    for (final k in getKeys()) {
      if (k.startsWith(fullPrefix)) {
        final symbol = k.substring(fullPrefix.length).trim().toUpperCase();
        if (symbol.isNotEmpty && !out.contains(symbol)) out.add(symbol);
      }
    }
    out.sort();
    return out;
  }

  List<int>? getIntList(String key) {
    final str = getString(key);
    if (str == null) return null;
    try {
      return (jsonDecode(str) as List).cast<int>();
    } catch (_) {
      return null;
    }
  }

  Future<bool> setIntList(String key, List<int> value) =>
      setString(key, jsonEncode(value));

  Map<int, int>? getIntIntMap(String key) {
    final str = getString(key);
    if (str == null) return null;
    try {
      final map = jsonDecode(str) as Map<String, dynamic>;
      return map.map((k, v) => MapEntry(int.parse(k), v as int));
    } catch (_) {
      return null;
    }
  }

  Future<bool> setIntIntMap(String key, Map<int, int> value) {
    final map = value.map((k, v) => MapEntry(k.toString(), v));
    return setString(key, jsonEncode(map));
  }
}

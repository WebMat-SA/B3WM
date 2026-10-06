/// Config central do app (uma fonte para a URL do backend).
///
/// Troca por plataforma via `--dart-define`:
/// `flutter run --dart-define=API_BASE_URL=https://192.168.1.10:5002`
/// (emulador Android: `http://10.0.2.2:5002`; físico: IP da máquina).
class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://localhost:5002',
  );

  static String get hubUrl => '$apiBaseUrl/api/datahub';
}

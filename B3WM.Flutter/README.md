# B3WM.Flutter — Frontend Map Flow Chart

Cliente **Flutter** (desktop/mobile) do B3WM: gráfico Map Flow em `CustomPainter` com candles, bubbles, volume profile, estruturas, topos/vales, pivot tradicional, VWAP, painel de análise diária (1D) e painel de trading (MT5 via backend).

> As imagens em [`../screenshots/`](../screenshots/) são geradas pelos golden tests. Quando `../B3WM/Data/` e as `shared_preferences` estão presentes, eles usam a sessão real (WINFUT, 2026-10-28); no CI/clone limpo, usam o fixture versionado `test/fixtures/readme_session.json`.

## Pré-requisitos

- Flutter SDK (testado com **Flutter 3.44 / Dart 3.12**)
- Backend rodando em `https://localhost:5002` (ver [README raiz](../README.md))
- `dotnet dev-certs https --trust` executado (certificado self-signed do backend)

## Rodando

```bash
cd B3WM.Flutter
flutter pub get
flutter run -d windows   # ou -d chrome / -d android
```

### Apontando para o backend

A URL base fica em `lib/main.dart` (`baseUrl`, padrão `https://localhost:5002`) e alimenta `ApiService` (REST), `TradingApiService` (trading via backend) e `SignalRService` (hub `/api/datahub`).

| Onde roda | `baseUrl` a usar |
|---|---|
| Mesmo PC (Windows/Chrome) | `https://localhost:5002` (com `dotnet dev-certs https --trust`) |
| Emulador Android | `https://10.0.2.2:5002` |
| Celular/outro PC na rede | `https://<IP-do-backend>:5002` (aceite o certificado) |

## Estrutura

```
lib/
├── main.dart                  # baseUrl + providers + NewMapFlowPage
├── services/                  # api_service, signalr_service, trading_service,
│                              # state_service, preferences_service, audio_service
├── models/                    # bar/bubble/structure/volume/extreme/pivot/daily configs
└── ui/widgets/
    ├── chart/                 # MapFlowChart (CustomPainter) + chart_data
    ├── app_bar_widget.dart    # atalhos p/ cada aba do drawer
    ├── app_drawer.dart        # 8 abas: Bubbles, Estrutura, Volume Profile,
    │                          # Topos/Vales, Pivot, VWAP, Backup, Estratégias
    ├── trading_drawer.dart    # painel lateral de trading (conta/ordens/posições/histórico)
    └── daily/                 # painel inferior 1D: structure/volume/extreme/pivot
test/
├── screenshots_golden_test.dart  # gera test/goldens/ (copiar p/ ../screenshots/)
├── readme_data.dart              # loader híbrido: dados reais + fallback do fixture
├── fixtures/readme_session.json  # sessão WINFUT 2026-10-28 versionada p/ CI
├── pivot_test.dart               # regras do pivot no client
└── ...                           # widget/unit tests (daily, structure, volume, state, backup)
assets/
├── sounds/ └── images/
```

## Testes e screenshots

```bash
flutter test                                   # todos os testes
flutter test test/screenshots_golden_test.dart --update-goldens   # regenera os PNGs
```

Depois copie `test/goldens/*.png` para `../screenshots/`. Cobertura: `overview`, `trading_panel`, `drawer_bubbles`, `drawer_estrutura`, `drawer_volume_profile`, `drawer_extreme`, `drawer_pivot`, `drawer_vwap`, `drawer_backup`, `drawer_strategy` e `daily_panel`. O Verifier está desabilitado e não possui golden.

Para forçar os dados reais, defina `B3WM_DATA_DIR` (pasta `Data/`) e opcionalmente `B3WM_PREFS_JSON` (arquivo `shared_preferences.json`). Sem eles, o loader procura `../B3WM/Data` e os caminhos padrão do `shared_preferences` (Windows/WSL/Linux) e, por fim, usa o fixture.

## Troubleshooting

- **Tela “Selecione um símbolo” travada / sem dados:** backend fora do ar ou `baseUrl` errado — confira `https://localhost:5002` no navegador.
- **Erro de certificado no Chrome/Android:** refaça `dotnet dev-certs https --trust` e ajuste o `baseUrl` pela tabela acima.
- **`flutter pub get` falhando:** confira a versão do Flutter (`flutter --version` ≈ 3.44 / Dart 3.12).
- **Golden sem dados / erro de fixture:** confira se `test/fixtures/readme_session.json` existe ou se `B3WM_DATA_DIR`/`B3WM_PREFS_JSON` apontam para caminhos válidos.

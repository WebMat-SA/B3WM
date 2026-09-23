# B3WM.Flutter — Frontend Map Flow Chart

Cliente **Flutter** (desktop/mobile) do B3WM: gráfico Map Flow em `CustomPainter` com candles, bubbles, volume profile, estruturas, topos/vales, pivot tradicional, VWAP, painel de análise diária (1D) e painel de trading (MT5 via backend).

> Pré-visualização sem dados reais: as imagens em [`../screenshots/`](../screenshots/) são geradas pelos golden tests com dados de exemplo.

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
    │                          # Topos/Vales, Pivot, VWAP, Período, Backup
    ├── trading_drawer.dart    # painel lateral de trading (conta/ordens/posições/histórico)
    └── daily/                 # painel inferior 1D: structure/volume/extreme/pivot
test/
├── screenshots_golden_test.dart  # gera test/goldens/ (copiar p/ ../screenshots/)
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

Depois copie `test/goldens/*.png` para `../screenshots/` (overview, drawer_bubbles, drawer_estrutura, drawer_volume_profile, drawer_extreme, drawer_pivot, trading_panel). O Verifier está desabilitado e não possui golden.

## Troubleshooting

- **Tela “Selecione um símbolo” travada / sem dados:** backend fora do ar ou `baseUrl` errado — confira `https://localhost:5002` no navegador.
- **Erro de certificado no Chrome/Android:** refaça `dotnet dev-certs https --trust` e ajuste o `baseUrl` pela tabela acima.
- **`flutter pub get` falhando:** confira a versão do Flutter (`flutter --version` ≈ 3.44 / Dart 3.12).
- **Pivot/Extreme vazios nos goldens:** esperado — os fakes não mockam esses endpoints, a screenshot mostra os controles e a mensagem de “sem dados”.

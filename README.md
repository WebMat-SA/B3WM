# B3WM — B3 Web Markets

## ⚠️ Aviso Legal / Disclaimer

> Este projeto é **exclusivamente para fins EDUCACIONAIS E DE ESTUDO** do mercado financeiro.
>
> **Não é permitido** o uso comercial, monetização ou distribuição com fins lucrativos.
> A coleta de dados utilizada por este software não autoriza tais usos.
>
> Todo e qualquer uso é de **inteira responsabilidade do usuário**.
> O autor não se responsabiliza por perdas financeiras, decisões de investimento
> ou qualquer dano decorrente do uso deste software.

---

## Sobre

Plataforma **open-source** de visualização em tempo real e estudo de microestrutura do mercado de futuros brasileiro (B3), focada nos contratos **WINFUT** (Mini Índice) e **WDOFUT** (Mini Dólar).

**Objetivo:** Estudar a dinâmica do fluxo de ordens, perfil de volume, agressividade dos participantes e estrutura de preços — tudo em tempo real.

---

## Funcionalidades

### Visualização em Tempo Real
- Gráfico de candles com múltiplos timeframes (1, 2, 5, 15, 30, 60 min)
- **Bubbles:** Trades agressivos (grandes volumes por mesmo agente) destacados no gráfico
- **Volume Profile:** Perfil de volume por nível de preço com POC (Point of Control)
- **Delta Profile:** Diferença compra-venda por nível (buying/selling pressure)
- **Estruturas de Suporte/Resistência:** Borders calculadas automaticamente com base na ação do preço

### Análise de Microestrutura
- Identificação de agentes compradores/vendedores por corretora
- Detecção de bubbles (sequências de mesmo agente agredindo)
- Análise de delta acumulado por nível de preço
- Reconstrução de perfil de volume por intervalo selecionado

### Backtest de Estratégias
- Motor de backtest server-side em .NET
- Estratégias baseadas em bubbles, volume profile e estrutura de preços
- Visualização dos trades no gráfico (entrada/saída com motivos)
- Métricas: Win Rate, Profit Factor, Drawdown, P&L

### Trading Automatizado (Integração MT5)
- Bridge Python/FastAPI para execução de ordens via MetaTrader 5
- Consulta de posições, saldo e informações de conta

---

## Stack Tecnológica

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 / ASP.NET Core / SignalR |
| Frontend | Blazor WebAssembly / MudBlazor 9 |
| Charting | ECharts (Vizor.ECharts wrapper) |
| Real-time | SignalR (WebSocket) |
| Trading Bridge | Python 3 / FastAPI / MetaTrader 5 |
| Persistência | JSON (arquivos) + IndexedDB (navegador) |
| Coleta de Dados | WPF / Excel RTD / Socket TCP (Profit) |

---

## Arquitetura

```
                    Profit Platform
                        │
   ┌────────────────────────────────────┐
   │      ExtractorRTD (WPF .NET)        │
   │  Socket TCP :12002 ou Excel RTD     │
   └──────────────┬─────────────────────┘
                  │ SignalR
                  v
   ┌────────────────────────────────────┐
   │    B3WM Server (ASP.NET Core)      │
   │                                    │
   │  CandleService → OHLCV            │
   │  BubbleService → Agressivos       │
   │  VolumeService → Perfil de Volume │
   │  StructureService → Suporte/Resis.│
   │  ThrottlingService → Snapshots    │
   └──────────┬────────────────────────┘
              │ REST + SignalR
              v
   ┌────────────────────────────────────┐
   │  Blazor WASM Client (Navegador)    │
   │  - NewMapFlow (Chart)             │
   │  - TradingDrawer (Ordens)         │
   │  - BacktestPage (Análise)         │
   └────────────────────────────────────┘

   ┌────────────────────────────────────┐
   │  Python/FastAPI Bridge            │
   │  → MetaTrader 5 (Ordens)          │
   └────────────────────────────────────┘
```

---

## Como Rodar

### Pré-requisitos
- .NET 10 SDK
- Python 3.12+ (opcional, para trading)
- MetaTrader 5 (opcional, para trading)
- Profit (Carteira Profissional) ou fonte de dados B3 (opcional, para dados reais)

### Servidor Web
```bash
dotnet run --project B3WM/B3WM
```
Acesse https://localhost:5002

### Python Bridge (opcional)
```bash
cd B3WM.Python
pip install -r requirements.txt
python main.py
```

### Coletor de Dados (ExtractorRTD)
Abra `ExtractorRTD/B3WM.ExtractorRTD.sln` no Visual Studio e compile. Necessário Profit Carteira Profissional rodando.

---

## Estrutura do Projeto

```
B3WM/                        # Solução principal (.NET)
├── B3WM/                   # Servidor ASP.NET Core + SignalR
│   ├── Services/Core/      # CandleService, BubbleService, VolumeService, StructureService
│   └── Services/Backtest/  # BacktestEngine, IStrategy, SmartBreakoutStrategy
├── B3WM.Client/            # Blazor WebAssembly
│   └── Pages/              # NewMapFlow, BacktestPage
├── B3WM.Shared/            # Modelos, DTOs, Extensions
├── B3WM.Python/            # Bridge MT5 (FastAPI)
└── ExtractorRTD/           # Coletor WPF (Profit)
```

---

## Licença

**GNU Affero General Public License v3.0 (AGPLv3)** — Uso exclusivamente educacional.

Este software é fornecido "como está", sem garantia de qualquer tipo.
O uso comercial ou monetização deste software é **expressamente proibido**.

### AGPLv3 em resumo
- ✅ **Estudo e aprendizado** — Livre para estudar, modificar e experimentar
- ✅ **Uso pessoal** — Pode usar para análise pessoal do mercado
- ⚠️ **Compartilhamento** — Se distribuir o código ou versões modificadas, deve manter a mesma licença AGPLv3
- ⚠️ **Serviços web** — Se rodar uma versão modificada como servidor web, **precisa disponibilizar o código fonte** aos usuários
- ❌ **Uso comercial fechado** — Não pode incorporar em produtos comerciais sem abrir o código
- ❌ **Monetização** — Não é permitido vender este software ou versões derivadas sem manter o código aberto

Veja o arquivo [LICENSE](LICENSE) para o texto completo.

---

## Aviso de Risco

Negociar futuros envolve risco significativo de perda financeira.
Este software **não** fornece recomendações de investimento, sinais de compra/venda
ou qualquer forma de aconselhamento financeiro.
**Use por sua conta e risco.**

---

## Autor

**Mateus Faria** — [GitHub](https://github.com/WebMat1)

# B3WM.Python — Bridge MetaTrader 5 (FastAPI)

Bridge **opcional** que expõe o MetaTrader 5 via HTTP para o servidor B3WM (`B3WM/Controllers/*` chamam este serviço através de `PythonService:BaseUrl`, padrão `http://localhost:8000` no `B3WM/appsettings.json`). O app Flutter nunca fala com este bridge diretamente — ele passa pelo backend (`/api/trade/*`).

> ⚠️ **Windows-only:** o pacote `MetaTrader5` só funciona no Windows com o terminal MT5 instalado e logado. Sem ele, o backend simplesmente opera sem trading.

## Pré-requisitos

- Windows + Python 3.12+
- MetaTrader 5 instalado, aberto e logado (conta demo serve)

## Rodando

```bash
cd B3WM.Python
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

Saúde: `GET http://localhost:8000/health` → `{"status": "ok", "mt5_connected": true/false}`.

## Endpoints (`main.py`)

| Método | Rota | Descrição |
|---|---|---|
| GET | `/health` | status + `mt5_connected` |
| POST | `/api/order/market` | ordem a mercado |
| POST | `/api/order/close` | fecha posição por ticket |
| POST | `/api/order/cancel` | cancela ordem pendente |
| POST | `/api/order/modify` | altera SL/TP |
| GET | `/api/account` | dados da conta |
| GET | `/api/orders` | ordens abertas |
| GET | `/api/positions[/{symbol}]` | posições (filtro opcional) |
| GET | `/api/history?symbol=&from_date=&to_date=` | deals históricos |
| GET | `/api/symbol/{symbol}` | info do símbolo (com rolagem WIN/IND/WDO/DOL) |

## Rolagem de contratos (`services/contract_utils.py`)

O frontend/backend usam os contínuos `WINFUT`/`INDFUT`/`WDOFUT`/`DOLFUT`; o bridge resolve para o contrato vigente a partir do registro `SYMBOL_ROOTS`:

| Canônico | Root MT5 | Vencimento |
|---|---|---|
| `WINFUT` | `WIN` | série par, quarta-feira próxima ao dia 15 |
| `INDFUT` | `IND` | série par, quarta-feira próxima ao dia 15 |
| `WDOFUT` | `WDO` | 1º dia útil do mês |
| `DOLFUT` | `DOL` | 1º dia útil do mês |

O nome negociável pode ter prefixo/sufixo do broker (ex.: `IND$`, `IND@N`).
Defina `MT5_SYMBOL_PREFIX` / `MT5_SYMBOL_SUFFIX` no ambiente do bridge ou
use `resolve_broker_symbol(..., prefix=, suffix=)`. Para descobrir o nome
exato na conta, rode no Windows:

```bash
python scripts/discover_symbols.py
```

## Testes

```bash
cd B3WM.Python
python -m unittest discover -s tests -v
```

## Estrutura

```
main.py                 # FastAPI + CORS + rotas (uvicorn :8000)
models/order.py         # AccountInfo, PositionInfo, OrderInfo, HistoryDeal, ...
models/ticks.py
services/order_executor.py   # MT5OrderExecutor (connect/market/close/modify/queries)
services/mt5.py
services/contract_utils.py   # registry + rolagem + resolve_broker_symbol
scripts/discover_symbols.py  # descoberta de IND/DOL no MT5 da corretora
tests/test_contract_utils.py # testes da rolagem (sem MT5)
requirements.txt        # fastapi, uvicorn, MetaTrader5, pydantic
```

## Troubleshooting

- **`mt5_connected: false`:** abra o MT5 e confirme login/conexão com a corretora; reinicie o uvicorn depois.
- **`pip install MetaTrader5` falhando:** você está no Linux/macOS — este bridge só instala no Windows.
- **Backend diz que o trading está fora:** confira `PythonService:BaseUrl` no `B3WM/appsettings.json` e se `http://localhost:8000/health` responde.
- **Símbolo não encontrado (`404`):** confira a rolagem do contrato vigente (`resolve_symbol`) e se o símbolo existe na sua corretora MT5.

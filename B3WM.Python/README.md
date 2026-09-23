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
| GET | `/api/symbol/{symbol}` | info do símbolo (com rolagem WIN/WDO) |

## Rolagem de contratos (`services/contract_utils.py`)

O frontend/backend usam os contínuos `WINFUT`/`WDOFUT`; o bridge resolve para o contrato vigente:

- `WDOFUT` → `WDO<X><AA>` (vence no 1º dia útil do mês)
- `WINFUT` → `WIN<X><AA>` (série par, vence na 4ª-feira próxima ao dia 15)

## Estrutura

```
main.py                 # FastAPI + CORS + rotas (uvicorn :8000)
models/order.py         # AccountInfo, PositionInfo, OrderInfo, HistoryDeal, ...
models/ticks.py
services/order_executor.py   # MT5OrderExecutor (connect/market/close/modify/queries)
services/mt5.py
services/contract_utils.py   # get_active_contract / resolve_symbol
requirements.txt        # fastapi, uvicorn, MetaTrader5, pydantic
```

## Troubleshooting

- **`mt5_connected: false`:** abra o MT5 e confirme login/conexão com a corretora; reinicie o uvicorn depois.
- **`pip install MetaTrader5` falhando:** você está no Linux/macOS — este bridge só instala no Windows.
- **Backend diz que o trading está fora:** confira `PythonService:BaseUrl` no `B3WM/appsettings.json` e se `http://localhost:8000/health` responde.
- **Símbolo não encontrado (`404`):** confira a rolagem do contrato vigente (`resolve_symbol`) e se o símbolo existe na sua corretora MT5.

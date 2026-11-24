import sys
import traceback

import MetaTrader5 as mt5

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from models.order import (
    AccountInfo,
    CloseOrderRequest,
    HistoryDeal,
    LimitOrderRequest,
    MarketOrderRequest,
    ModifyOrderRequest,
    ModifyPendingOrderRequest,
    OrderInfo,
    OrderResult,
    PositionInfo,
    SymbolInfo,
)
from services.order_executor import MT5OrderExecutor

app = FastAPI(title="B3WM Python MT5 Bridge")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

executor = MT5OrderExecutor()


@app.on_event("startup")
async def startup():
    if not executor.connect():
        error_code, error_desc = mt5.last_error()
        print(f"Failed to initialize MetaTrader 5", file=sys.stderr)
        print(f"Error: ({error_code}) {error_desc}", file=sys.stderr)


@app.on_event("shutdown")
async def shutdown():
    executor.shutdown()


@app.get("/health")
async def health():
    return {"status": "ok", "mt5_connected": executor.is_connected}


def _order_failure(exc: Exception) -> OrderResult:
    """Converte uma exceção do bridge em OrderResult (HTTP 200).

    O Flutter trata status != 200 como "falha ao enviar" sem motivo. Devolver
    200 com `success=False` mantém a mensagem real visível na UI.
    """
    traceback.print_exc()
    return OrderResult(
        success=False,
        retcode=-1,
        retcode_name="bridge_exception",
        message=f"Erro no bridge MT5: {exc}",
    )


@app.post("/api/order/market", response_model=OrderResult)
async def place_market_order(request: MarketOrderRequest):
    try:
        return executor.market_order(request)
    except Exception as exc:
        return _order_failure(exc)


@app.post("/api/order/limit", response_model=OrderResult)
async def place_limit_order(request: LimitOrderRequest):
    try:
        return executor.limit_order(request)
    except Exception as exc:
        return _order_failure(exc)


@app.post("/api/order/close", response_model=OrderResult)
async def close_position(request: CloseOrderRequest):
    try:
        return executor.close_position(request.position_ticket)
    except Exception as exc:
        return _order_failure(exc)


@app.post("/api/order/cancel", response_model=OrderResult)
async def cancel_order(request: CloseOrderRequest):
    try:
        return executor.cancel_order(request.position_ticket)
    except Exception as exc:
        return _order_failure(exc)


@app.post("/api/order/modify", response_model=OrderResult)
async def modify_position(request: ModifyOrderRequest):
    try:
        return executor.modify_position(
            position_ticket=request.position_ticket,
            sl=request.sl,
            tp=request.tp,
        )
    except Exception as exc:
        return _order_failure(exc)


@app.post("/api/order/modify-pending", response_model=OrderResult)
async def modify_pending_order(request: ModifyPendingOrderRequest):
    try:
        return executor.modify_pending_order(
            order_ticket=request.order_ticket,
            price=request.price,
            sl=request.sl,
            tp=request.tp,
        )
    except Exception as exc:
        return _order_failure(exc)


@app.get("/api/account", response_model=AccountInfo)
async def get_account_info():
    info = executor.get_account_info()
    if info is None:
        raise HTTPException(status_code=500, detail="Failed to get account info")
    return info


@app.get("/api/orders", response_model=list[OrderInfo])
async def get_orders():
    return executor.get_orders()


@app.get("/api/positions", response_model=list[PositionInfo])
@app.get("/api/positions/{symbol}", response_model=list[PositionInfo])
async def get_positions(symbol: str = ""):
    return executor.get_positions(symbol)


@app.get("/api/history", response_model=list[HistoryDeal])
async def get_history(symbol: str = "", from_date: str = "", to_date: str = ""):
    return executor.get_history_deals(symbol, from_date, to_date)


@app.get("/api/symbol/{symbol}", response_model=SymbolInfo)
async def get_symbol_info(symbol: str):
    try:
        # O executor resolve o contrato ativo + prefixo/sufixo do broker.
        info = executor.get_symbol_info(symbol)
        if info is None:
            raise HTTPException(status_code=404, detail=f"Symbol {symbol} not found")
        info.symbol = symbol
        return info
    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc(file=sys.stderr)
        raise HTTPException(status_code=500, detail=f"Internal error processing symbol {symbol}: {e}")

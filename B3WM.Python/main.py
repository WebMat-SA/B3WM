import sys
import traceback

import MetaTrader5 as mt5

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from models.order import (
    AccountInfo,
    CloseOrderRequest,
    MarketOrderRequest,
    ModifyOrderRequest,
    OrderResult,
    PositionInfo,
    SymbolInfo,
)
from services.contract_utils import resolve_symbol
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


@app.post("/api/order/market", response_model=OrderResult)
async def place_market_order(request: MarketOrderRequest):
    result = executor.market_order(request)
    return result


@app.post("/api/order/close", response_model=OrderResult)
async def close_position(request: CloseOrderRequest):
    result = executor.close_position(request.position_ticket)
    return result


@app.post("/api/order/modify", response_model=OrderResult)
async def modify_position(request: ModifyOrderRequest):
    result = executor.modify_position(
        position_ticket=request.position_ticket,
        sl=request.sl,
        tp=request.tp,
    )
    return result


@app.get("/api/account", response_model=AccountInfo)
async def get_account_info():
    info = executor.get_account_info()
    if info is None:
        raise HTTPException(status_code=500, detail="Failed to get account info")
    return info


@app.get("/api/positions", response_model=list[PositionInfo])
@app.get("/api/positions/{symbol}", response_model=list[PositionInfo])
async def get_positions(symbol: str = ""):
    return executor.get_positions(symbol)


@app.get("/api/symbol/{symbol}", response_model=SymbolInfo)
async def get_symbol_info(symbol: str):
    try:
        resolved = resolve_symbol(symbol)
        info = executor.get_symbol_info(resolved)
        if info is None:
            raise HTTPException(status_code=404, detail=f"Symbol {symbol} not found")
        if resolved != symbol:
            info.symbol = symbol
        return info
    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc(file=sys.stderr)
        raise HTTPException(status_code=500, detail=f"Internal error processing symbol {symbol}: {e}")

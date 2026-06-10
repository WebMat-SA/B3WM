from __future__ import annotations

from datetime import datetime
from typing import Optional

from pydantic import BaseModel


class MarketOrderRequest(BaseModel):
    symbol: str
    volume: float
    type: str  # "buy" or "sell"
    sl: Optional[float] = None
    tp: Optional[float] = None
    comment: str = ""
    magic: int = 0
    deviation: int = 10


class OrderResult(BaseModel):
    success: bool = False
    retcode: int = -1
    retcode_name: str = ""
    order_ticket: int = 0
    price: float = 0.0
    volume: float = 0.0
    message: str = ""


class CloseOrderRequest(BaseModel):
    position_ticket: int


class ModifyOrderRequest(BaseModel):
    position_ticket: int
    sl: Optional[float] = None
    tp: Optional[float] = None


class AccountInfo(BaseModel):
    login: int = 0
    balance: float = 0.0
    equity: float = 0.0
    profit: float = 0.0
    margin: float = 0.0
    margin_free: float = 0.0
    margin_level: float = 0.0
    leverage: int = 0
    currency: str = ""
    server: str = ""
    trade_allowed: bool = False
    name: str = ""


class PositionInfo(BaseModel):
    ticket: int = 0
    symbol: str = ""
    type: str = ""  # "buy" or "sell"
    volume: float = 0.0
    price_open: float = 0.0
    sl: float = 0.0
    tp: float = 0.0
    price_current: float = 0.0
    profit: float = 0.0
    swap: float = 0.0
    commission: float = 0.0
    magic: int = 0
    comment: str = ""
    time: str = ""


class SymbolInfo(BaseModel):
    symbol: str = ""
    bid: float = 0.0
    ask: float = 0.0
    spread: int = 0
    digits: int = 0
    trade_mode: str = ""
    volume_min: float = 0.0
    volume_max: float = 0.0
    volume_step: float = 0.0
    point: float = 0.0
    tick_value: float = 0.0
    contract_size: float = 0.0
    description: str = ""

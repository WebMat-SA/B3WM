import sys
from typing import Optional

import MetaTrader5 as mt5

from models.order import (
    AccountInfo,
    MarketOrderRequest,
    OrderResult,
    PositionInfo,
    SymbolInfo,
)
from services.contract_utils import resolve_symbol


class MT5OrderExecutor:
    def __init__(self):
        self._connected = False

    def connect(self, path: str = "", login: int = 0, password: str = "", server: str = "") -> bool:
        if self._connected:
            return True

        kwargs = {}
        if path:
            kwargs["path"] = path
        if login:
            kwargs["login"] = login
            kwargs["password"] = password
            kwargs["server"] = server

        initialized = mt5.initialize(**kwargs) if kwargs else mt5.initialize()
        if not initialized:
            return False

        self._connected = True
        return True

    def shutdown(self) -> None:
        if self._connected:
            mt5.shutdown()
            self._connected = False

    @property
    def is_connected(self) -> bool:
        return self._connected

    def ensure_connected(self) -> bool:
        if not self._connected:
            return self.connect()

        probe = mt5.account_info()
        if probe is not None:
            return True

        error_code, error_desc = mt5.last_error()
        if error_code == -10004:
            print(f"[MT5] IPC lost ({error_code} {error_desc}). Reconnecting...", file=sys.stderr)
            mt5.shutdown()
            self._connected = False
            return self.connect()

        return True

    def _resolve_sym(self, symbol: str) -> str:
        return resolve_symbol(symbol)

    def market_order(self, request: MarketOrderRequest) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        symbol = self._resolve_sym(request.symbol)
        mt5_type = mt5.ORDER_TYPE_BUY if request.type.lower() == "buy" else mt5.ORDER_TYPE_SELL

        tick = mt5.symbol_info_tick(symbol)
        if tick is None:
            return OrderResult(message=f"Symbol {request.symbol} not found")

        price = tick.ask if mt5_type == mt5.ORDER_TYPE_BUY else tick.bid

        mt5_request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": symbol,
            "volume": request.volume,
            "type": mt5_type,
            "price": price,
            "deviation": request.deviation,
            "magic": request.magic,
            "comment": request.comment,
            "type_time": mt5.ORDER_TIME_GTC,
            "type_filling": mt5.ORDER_FILLING_RETURN,
        }

        if request.sl is not None:
            mt5_request["sl"] = request.sl
        if request.tp is not None:
            mt5_request["tp"] = request.tp

        result = mt5.order_send(mt5_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=result.retcode == mt5.TRADE_RETCODE_DONE,
            retcode=result.retcode,
            retcode_name=retcode_name,
            order_ticket=result.order,
            price=result.price,
            volume=result.volume,
            message=result.comment or retcode_name,
        )

    def close_position(self, position_ticket: int) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        position = mt5.positions_get(ticket=position_ticket)
        if position is None or len(position) == 0:
            return OrderResult(message=f"Position {position_ticket} not found")

        position = position[0]
        mt5_type = mt5.ORDER_TYPE_SELL if position.type == mt5.ORDER_TYPE_BUY else mt5.ORDER_TYPE_BUY

        tick = mt5.symbol_info_tick(position.symbol)
        if tick is None:
            return OrderResult(message=f"Symbol {position.symbol} not found")

        price = tick.bid if mt5_type == mt5.ORDER_TYPE_SELL else tick.ask

        close_request = {
            "action": mt5.TRADE_ACTION_DEAL,
            "symbol": position.symbol,
            "volume": position.volume,
            "type": mt5_type,
            "position": position_ticket,
            "price": price,
            "deviation": 10,
            "magic": position.magic,
            "comment": "close by python",
            "type_time": mt5.ORDER_TIME_GTC,
            "type_filling": mt5.ORDER_FILLING_RETURN,
        }

        result = mt5.order_send(close_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=result.retcode == mt5.TRADE_RETCODE_DONE,
            retcode=result.retcode,
            retcode_name=retcode_name,
            order_ticket=result.order,
            price=result.price,
            volume=result.volume,
            message=result.comment or retcode_name,
        )

    def modify_position(self, position_ticket: int, sl: Optional[float] = None, tp: Optional[float] = None) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        position = mt5.positions_get(ticket=position_ticket)
        if position is None or len(position) == 0:
            return OrderResult(message=f"Position {position_ticket} not found")

        position = position[0]
        new_sl = sl if sl is not None else position.sl
        new_tp = tp if tp is not None else position.tp

        modify_request = {
            "action": mt5.TRADE_ACTION_SLTP,
            "symbol": position.symbol,
            "position": position_ticket,
            "sl": new_sl,
            "tp": new_tp,
            "magic": position.magic,
            "comment": "modify by python",
        }

        result = mt5.order_send(modify_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=result.retcode == mt5.TRADE_RETCODE_DONE,
            retcode=result.retcode,
            retcode_name=retcode_name,
            message=retcode_name,
        )

    def get_account_info(self) -> Optional[AccountInfo]:
        if not self.ensure_connected():
            return None

        info = mt5.account_info()
        if info is None:
            error_code, error_desc = mt5.last_error()
            print(f"[MT5] account_info() returned None. last_error: ({error_code}) {error_desc}", file=sys.stderr)
            return None

        return AccountInfo(
            login=info.login,
            balance=info.balance,
            equity=info.equity,
            profit=info.profit,
            margin=info.margin,
            margin_free=info.margin_free,
            margin_level=info.margin_level,
            leverage=info.leverage,
            currency=info.currency,
            server=info.server,
            trade_allowed=info.trade_allowed,
            name=info.name,
        )

    def get_positions(self, symbol: str = "") -> list[PositionInfo]:
        if not self.ensure_connected():
            return []

        if symbol:
            symbol = self._resolve_sym(symbol)
            positions = mt5.positions_get(symbol=symbol)
        else:
            positions = mt5.positions_get()

        if positions is None:
            return []

        result = []
        for p in positions:
            result.append(PositionInfo(
                ticket=p.ticket,
                symbol=p.symbol,
                type="buy" if p.type == mt5.ORDER_TYPE_BUY else "sell",
                volume=p.volume,
                price_open=p.price_open,
                sl=p.sl,
                tp=p.tp,
                price_current=p.price_current,
                profit=p.profit,
                swap=p.swap,
                commission=getattr(p, 'commission', 0.0),
                magic=p.magic,
                comment=p.comment,
                time=str(p.time),
            ))
        return result

    def get_symbol_info(self, symbol: str) -> Optional[SymbolInfo]:
        if not self.ensure_connected():
            return None

        try:
            symbol = self._resolve_sym(symbol)
            tick = mt5.symbol_info_tick(symbol)
            info = mt5.symbol_info(symbol)
        except Exception as e:
            print(f"[MT5] exception in symbol_info for {symbol}: {e}", file=sys.stderr)
            return None

        if info is None:
            return None

        trade_mode_map = {
            mt5.SYMBOL_TRADE_MODE_DISABLED: "disabled",
            mt5.SYMBOL_TRADE_MODE_LONGONLY: "long_only",
            mt5.SYMBOL_TRADE_MODE_SHORTONLY: "short_only",
            mt5.SYMBOL_TRADE_MODE_CLOSEONLY: "close_only",
            mt5.SYMBOL_TRADE_MODE_FULL: "full",
        }

        try:
            return SymbolInfo(
                symbol=symbol,
                bid=tick.bid if tick else 0.0,
                ask=tick.ask if tick else 0.0,
                spread=info.spread,
                digits=info.digits,
                trade_mode=trade_mode_map.get(info.trade_mode, "unknown"),
                volume_min=getattr(info, "volume_min", 0.0),
                volume_max=getattr(info, "volume_max", 0.0),
                volume_step=getattr(info, "volume_step", 0.0),
                point=info.point,
                tick_value=getattr(info, "trade_tick_value", 0.0),
                contract_size=getattr(info, "contract_size", 0),
                description=getattr(info, "description", ""),
            )
        except Exception as e:
            print(f"[MT5] error building SymbolInfo for {symbol}: {e}", file=sys.stderr)
            return None

    def _retcode_name(self, retcode: int) -> str:
        mapping = {
            mt5.TRADE_RETCODE_REQUOTE: "requote",
            mt5.TRADE_RETCODE_REJECT: "reject",
            mt5.TRADE_RETCODE_CANCEL: "cancel",
            mt5.TRADE_RETCODE_PLACED: "placed",
            mt5.TRADE_RETCODE_DONE: "done",
            mt5.TRADE_RETCODE_DONE_PARTIAL: "done_partial",
            mt5.TRADE_RETCODE_ERROR: "error",
            mt5.TRADE_RETCODE_TIMEOUT: "timeout",
            mt5.TRADE_RETCODE_INVALID: "invalid",
            mt5.TRADE_RETCODE_INVALID_VOLUME: "invalid_volume",
            mt5.TRADE_RETCODE_INVALID_PRICE: "invalid_price",
            mt5.TRADE_RETCODE_INVALID_STOPS: "invalid_stops",
            mt5.TRADE_RETCODE_TRADE_DISABLED: "trade_disabled",
            mt5.TRADE_RETCODE_MARKET_CLOSED: "market_closed",
            mt5.TRADE_RETCODE_NO_MONEY: "no_money",
            mt5.TRADE_RETCODE_PRICE_CHANGED: "price_changed",
            mt5.TRADE_RETCODE_PRICE_OFF: "price_off",
            mt5.TRADE_RETCODE_INVALID_EXPIRATION: "invalid_expiration",
            mt5.TRADE_RETCODE_ORDER_CHANGED: "order_changed",
            mt5.TRADE_RETCODE_TOO_MANY_REQUESTS: "too_many_requests",
            mt5.TRADE_RETCODE_NO_CHANGES: "no_changes",
            mt5.TRADE_RETCODE_SERVER_DISABLES_AT: "server_disables_at",
            mt5.TRADE_RETCODE_CLIENT_DISABLES_AT: "client_disables_at",
            mt5.TRADE_RETCODE_LOCKED: "locked",
            mt5.TRADE_RETCODE_FROZEN: "frozen",
            mt5.TRADE_RETCODE_INVALID_FILL: "invalid_fill",
            mt5.TRADE_RETCODE_CONNECTION: "connection",
            mt5.TRADE_RETCODE_ONLY_REAL: "only_real",
            mt5.TRADE_RETCODE_LIMIT_ORDERS: "limit_orders",
            mt5.TRADE_RETCODE_LIMIT_VOLUME: "limit_volume",
            mt5.TRADE_RETCODE_INVALID_ORDER: "invalid_order",
            mt5.TRADE_RETCODE_POSITION_CLOSED: "position_closed",
            mt5.TRADE_RETCODE_INVALID_CLOSE_VOLUME: "invalid_close_volume",
            mt5.TRADE_RETCODE_CLOSE_ORDER_EXIST: "close_order_exist",
            mt5.TRADE_RETCODE_LIMIT_POSITIONS: "limit_positions",
            mt5.TRADE_RETCODE_REINITIALIZE: "reinitialize",
            mt5.TRADE_RETCODE_HEDGE_PROHIBITED: "hedge_prohibited",
        }
        return mapping.get(retcode, f"unknown_{retcode}")

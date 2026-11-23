import os
import sys
from datetime import datetime, timedelta, timezone
from typing import Optional

import MetaTrader5 as mt5

from models.order import (
    AccountInfo,
    HistoryDeal,
    LimitOrderRequest,
    MarketOrderRequest,
    OrderBook,
    OrderBookLevel,
    OrderInfo,
    OrderResult,
    PositionInfo,
    SymbolInfo,
)
from services.contract_utils import (
    get_symbol_prefix,
    resolve_broker_symbol,
)


class MT5OrderExecutor:
    def __init__(self):
        self._connected = False
        # Símbolos com subscrição ativa de Depth of Market (market_book_add).
        self._book_symbols: set[str] = set()
        # Evita repetir o log de falha a cada poll quando a corretora não
        # expõe book para o símbolo.
        self._book_add_warned: set[str] = set()

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
        for symbol in list(self._book_symbols):
            try:
                mt5.market_book_release(symbol)
            except Exception:
                pass
        self._book_symbols.clear()
        self._book_add_warned.clear()
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
            self._book_symbols.clear()
            self._book_add_warned.clear()
            mt5.shutdown()
            self._connected = False
            return self.connect()

        return True

    def _resolve_sym(self, symbol: str) -> str:
        # Mapeia o nome canônico (INDFUT) no contrato ativo e aplica o
        # prefixo/sufixo opcional do broker (issue #47).
        return resolve_broker_symbol(
            symbol,
            prefix=os.getenv("MT5_SYMBOL_PREFIX", ""),
            suffix=os.getenv("MT5_SYMBOL_SUFFIX", ""),
            exists=lambda s: mt5.symbol_info(s) is not None,
        )

    def market_order(self, request: MarketOrderRequest) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        symbol = self._resolve_sym(request.symbol)
        mt5_type = mt5.ORDER_TYPE_BUY if request.type.lower() == "buy" else mt5.ORDER_TYPE_SELL

        tick = mt5.symbol_info_tick(symbol)
        if tick is None:
            return OrderResult(message=f"Symbol {request.symbol} not found")
        info = mt5.symbol_info(symbol)

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
            "type_filling": self._filling_mode(info),
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
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            order_ticket=int(result.order or 0),
            price=float(result.price or 0),
            volume=float(result.volume or 0),
            message=str(result.comment or retcode_name),
        )

    def limit_order(self, request: LimitOrderRequest) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        symbol = self._resolve_sym(request.symbol)
        order_type = request.type.lower()
        if order_type not in ("buy", "sell"):
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID,
                retcode_name="invalid",
                message="Tipo de ordem inválido (use buy ou sell).",
            )
        is_buy = order_type == "buy"
        order_kind = (request.kind or "limit").lower()
        if order_kind not in ("limit", "stop"):
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID,
                retcode_name="invalid",
                message="Tipo de pendente inválido (use limit ou stop).",
            )

        tick = mt5.symbol_info_tick(symbol)
        info = mt5.symbol_info(symbol)
        if tick is None or info is None:
            return OrderResult(message=f"Symbol {request.symbol} not found")

        volume_min = getattr(info, "volume_min", 0.0) or 0.0
        if request.volume <= 0 or request.volume < volume_min:
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID_VOLUME,
                retcode_name="invalid_volume",
                message=(
                    f"Volume inválido para a ordem limite (mínimo "
                    f"{volume_min:g})."
                ),
            )

        if tick.ask <= 0 or tick.bid <= 0:
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_MARKET_CLOSED,
                retcode_name="market_closed",
                message="Sem cotação disponível (mercado fechado).",
            )

        digits = info.digits
        price = round(request.price, digits)
        if price <= 0:
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                retcode_name="invalid_price",
                message="Preço inválido para a ordem limite.",
            )

        stops_level = (getattr(info, "trade_stops_level", 0) or 0) * (
            getattr(info, "point", 0.0) or 0.0
        )
        if is_buy:
            if order_kind == "limit":
                mt5_type = mt5.ORDER_TYPE_BUY_LIMIT
                limit_price = tick.ask - stops_level
                if price >= limit_price:
                    return OrderResult(
                        success=False,
                        retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                        retcode_name="invalid_price",
                        message=(
                            "Ordem de compra limite rejeitada: o preço "
                            f"{price:.{digits}f} deve ficar abaixo do preço atual "
                            f"{tick.ask:.{digits}f}."
                        ),
                    )
            else:
                mt5_type = mt5.ORDER_TYPE_BUY_STOP
                stop_price = tick.ask + stops_level
                if price <= stop_price:
                    return OrderResult(
                        success=False,
                        retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                        retcode_name="invalid_price",
                        message=(
                            "Ordem de compra stop rejeitada: o preço "
                            f"{price:.{digits}f} deve ficar acima do preço atual "
                            f"{tick.ask:.{digits}f}."
                        ),
                    )
        else:
            if order_kind == "limit":
                mt5_type = mt5.ORDER_TYPE_SELL_LIMIT
                limit_price = tick.bid + stops_level
                if price <= limit_price:
                    return OrderResult(
                        success=False,
                        retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                        retcode_name="invalid_price",
                        message=(
                            "Ordem de venda limite rejeitada: o preço "
                            f"{price:.{digits}f} deve ficar acima do preço atual "
                            f"{tick.bid:.{digits}f}."
                        ),
                    )
            else:
                mt5_type = mt5.ORDER_TYPE_SELL_STOP
                stop_price = tick.bid - stops_level
                if price >= stop_price:
                    return OrderResult(
                        success=False,
                        retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                        retcode_name="invalid_price",
                        message=(
                            "Ordem de venda stop rejeitada: o preço "
                            f"{price:.{digits}f} deve ficar abaixo do preço atual "
                            f"{tick.bid:.{digits}f}."
                        ),
                    )

        type_time, expiration = self._pending_expiration(info, tick.time)
        mt5_request = {
            "action": mt5.TRADE_ACTION_PENDING,
            "symbol": symbol,
            "volume": request.volume,
            "type": mt5_type,
            "price": price,
            "magic": request.magic,
            "comment": request.comment,
            "type_time": type_time,
            "type_filling": self._filling_mode(info),
        }
        if expiration:
            mt5_request["expiration"] = expiration

        if request.sl is not None:
            mt5_request["sl"] = request.sl
        if request.tp is not None:
            mt5_request["tp"] = request.tp

        result = mt5.order_send(mt5_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            order_ticket=int(result.order or 0),
            price=float(result.price or 0),
            volume=float(result.volume or 0),
            message=str(result.comment or retcode_name),
        )

    def modify_pending_order(
        self,
        order_ticket: int,
        price: float,
        sl: Optional[float] = None,
        tp: Optional[float] = None,
    ) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        orders = mt5.orders_get(ticket=order_ticket)
        if orders is None or len(orders) == 0:
            return OrderResult(message=f"Order {order_ticket} not found")

        order = orders[0]
        pending_types = (
            mt5.ORDER_TYPE_BUY_LIMIT,
            mt5.ORDER_TYPE_SELL_LIMIT,
            mt5.ORDER_TYPE_BUY_STOP,
            mt5.ORDER_TYPE_SELL_STOP,
            mt5.ORDER_TYPE_BUY_STOP_LIMIT,
            mt5.ORDER_TYPE_SELL_STOP_LIMIT,
        )
        if order.type not in pending_types:
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID,
                retcode_name="invalid",
                message="A ordem informada não é uma ordem pendente.",
            )

        info = mt5.symbol_info(order.symbol)
        digits = info.digits if info is not None else 2
        new_price = round(price, digits)
        if new_price <= 0:
            return OrderResult(
                success=False,
                retcode=mt5.TRADE_RETCODE_INVALID_PRICE,
                retcode_name="invalid_price",
                message="Preço inválido para modificar a ordem.",
            )

        specified_modes = (
            getattr(mt5, "ORDER_TIME_SPECIFIED", 2),
            getattr(mt5, "ORDER_TIME_SPECIFIED_DAY", 3),
        )
        type_time = order.type_time
        expiration = int(getattr(order, "time_expiration", 0) or 0)
        if type_time in specified_modes:
            tick = mt5.symbol_info_tick(order.symbol)
            server_now = tick.time if tick is not None else 0
            if expiration <= 0:
                type_time, expiration = self._pending_expiration(
                    info, server_now
                )
        else:
            expiration = 0

        modify_request = {
            "action": mt5.TRADE_ACTION_MODIFY,
            "order": order_ticket,
            "symbol": order.symbol,
            "volume": order.volume_current,
            "type": order.type,
            "price": new_price,
            "sl": sl if sl is not None else order.sl,
            "tp": tp if tp is not None else order.tp,
            "magic": order.magic,
            "comment": "modify pending by python",
            "type_time": type_time,
            "type_filling": order.type_filling,
        }
        if expiration:
            modify_request["expiration"] = expiration

        result = mt5.order_send(modify_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            order_ticket=int(result.order or order_ticket),
            price=float(result.price or new_price),
            volume=float(result.volume or order.volume_current),
            message=str(result.comment or retcode_name),
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
        info = mt5.symbol_info(position.symbol)

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
            "type_filling": self._filling_mode(info),
        }

        result = mt5.order_send(close_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            order_ticket=int(result.order or 0),
            price=float(result.price or 0),
            volume=float(result.volume or 0),
            message=str(result.comment or retcode_name),
        )

    def cancel_order(self, order_ticket: int) -> OrderResult:
        if not self.ensure_connected():
            return OrderResult(message="MT5 not connected")

        cancel_request = {
            "action": mt5.TRADE_ACTION_REMOVE,
            "order": order_ticket,
        }

        result = mt5.order_send(cancel_request)
        if result is None:
            return OrderResult(message="order_send returned None")

        retcode_name = self._retcode_name(result.retcode)
        return OrderResult(
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            message=retcode_name,
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
            success=self._is_success(result.retcode),
            retcode=int(result.retcode or 0),
            retcode_name=retcode_name,
            message=retcode_name,
        )

    def get_order_book(self, symbol: str, depth: int = 0) -> OrderBook:
        """Book de ofertas agregado por preço.

        Usa `mt5.market_book_get` quando disponível. Se a corretora/B3 não
        expuser o book, cai para um Nível 1 sintético a partir do tick,
        sinalizando explicitamente `source="tick_fallback"`.
        """
        now = datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
        if not self.ensure_connected():
            return OrderBook(
                symbol=symbol,
                time=now,
                available=False,
                source="unavailable",
                message="MT5 não conectado.",
            )

        resolved = self._resolve_sym(symbol)
        mt5.symbol_select(resolved, True)

        # market_book_get só retorna dados se houver subscrição prévia via
        # market_book_add. Sem isso o MT5 devolve None e o book cai no
        # fallback Nível 1 (uma única linha, sem volumes).
        self._subscribe_book(resolved)
        book = self._read_book(resolved)
        if book is None:
            # A subscrição pode ter caído (reconexão/troca de símbolo).
            self._book_symbols.discard(resolved)
            self._subscribe_book(resolved)
            book = self._read_book(resolved)

        if book:
            bids: dict[float, float] = {}
            asks: dict[float, float] = {}
            for item in book:
                volume = float(
                    getattr(item, "volume_dbl", 0)
                    or getattr(item, "volume", 0)
                    or 0
                )
                price = float(getattr(item, "price", 0) or 0)
                if price <= 0 or volume <= 0:
                    continue
                if item.type == mt5.BOOK_TYPE_SELL:
                    asks[price] = asks.get(price, 0.0) + volume
                elif item.type == mt5.BOOK_TYPE_BUY:
                    bids[price] = bids.get(price, 0.0) + volume

            if bids or asks:
                bid_levels = self._book_levels(bids, reverse=True, depth=depth)
                ask_levels = self._book_levels(asks, reverse=False, depth=depth)
                if bid_levels or ask_levels:
                    return OrderBook(
                        symbol=symbol,
                        time=now,
                        bids=bid_levels,
                        asks=ask_levels,
                        available=True,
                        source="book",
                    )

        tick = mt5.symbol_info_tick(resolved)
        if tick is not None and tick.bid > 0 and tick.ask > 0:
            return OrderBook(
                symbol=symbol,
                time=now,
                bids=[OrderBookLevel(price=tick.bid, volume=0.0)],
                asks=[OrderBookLevel(price=tick.ask, volume=0.0)],
                available=True,
                source="tick_fallback",
                message="Book indisponível; exibindo Nível 1 (bid/ask).",
            )

        return OrderBook(
            symbol=symbol,
            time=now,
            available=False,
            source="unavailable",
            message="Book indisponível e sem cotação (mercado fechado).",
        )

    @staticmethod
    def _read_book(symbol: str):
        try:
            return mt5.market_book_get(symbol)
        except Exception as e:
            print(f"[MT5] market_book_get({symbol}) failed: {e}", file=sys.stderr)
            return None

    def _subscribe_book(self, symbol: str) -> bool:
        if symbol in self._book_symbols:
            return True
        try:
            if mt5.market_book_add(symbol):
                self._book_symbols.add(symbol)
                self._book_add_warned.discard(symbol)
                return True
            error_code, error_desc = mt5.last_error()
            if symbol not in self._book_add_warned:
                self._book_add_warned.add(symbol)
                print(
                    f"[MT5] market_book_add({symbol}) failed: "
                    f"({error_code}) {error_desc}",
                    file=sys.stderr,
                )
        except Exception as e:
            if symbol not in self._book_add_warned:
                self._book_add_warned.add(symbol)
                print(
                    f"[MT5] market_book_add({symbol}) failed: {e}",
                    file=sys.stderr,
                )
        return False

    @staticmethod
    def _book_levels(
        levels: dict[float, float], *, reverse: bool, depth: int = 0
    ) -> list[OrderBookLevel]:
        ordered = sorted(levels.items(), key=lambda kv: kv[0], reverse=reverse)
        if depth and depth > 0:
            ordered = ordered[:depth]
        return [OrderBookLevel(price=price, volume=volume) for price, volume in ordered]

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

    def get_orders(self) -> list[OrderInfo]:
        if not self.ensure_connected():
            return []

        orders = mt5.orders_get()
        if orders is None:
            return []

        type_map = {
            mt5.ORDER_TYPE_BUY_LIMIT: "buy_limit",
            mt5.ORDER_TYPE_SELL_LIMIT: "sell_limit",
            mt5.ORDER_TYPE_BUY_STOP: "buy_stop",
            mt5.ORDER_TYPE_SELL_STOP: "sell_stop",
            mt5.ORDER_TYPE_BUY_STOP_LIMIT: "buy_stop_limit",
            mt5.ORDER_TYPE_SELL_STOP_LIMIT: "sell_stop_limit",
        }
        state_map = {
            mt5.ORDER_STATE_STARTED: "started",
            mt5.ORDER_STATE_PLACED: "placed",
            mt5.ORDER_STATE_CANCELED: "canceled",
            mt5.ORDER_STATE_PARTIAL: "partial",
            mt5.ORDER_STATE_FILLED: "filled",
            mt5.ORDER_STATE_REJECTED: "rejected",
            mt5.ORDER_STATE_EXPIRED: "expired",
            mt5.ORDER_STATE_REQUEST_ADD: "request_add",
            mt5.ORDER_STATE_REQUEST_MODIFY: "request_modify",
            mt5.ORDER_STATE_REQUEST_CANCEL: "request_cancel",
        }

        result = []
        for o in orders:
            result.append(OrderInfo(
                ticket=o.ticket,
                symbol=o.symbol,
                type=type_map.get(o.type, f"unknown_{o.type}"),
                volume=o.volume_current,
                price_open=o.price_open,
                sl=o.sl,
                tp=o.tp,
                time_setup=str(o.time_setup),
                time_expiration=str(o.time_expiration),
                state=state_map.get(o.state, f"unknown_{o.state}"),
                comment=o.comment,
                magic=o.magic,
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

    def get_history_deals(self, symbol: str = "", from_date: str = "", to_date: str = "") -> list[HistoryDeal]:
        if not self.ensure_connected():
            return []

        try:
            from_dt = datetime.fromisoformat(from_date) if from_date else datetime.now() - timedelta(days=30)
            to_dt = datetime.fromisoformat(to_date) if to_date else datetime.now()
        except Exception:
            return []

        prefix = get_symbol_prefix(symbol) if symbol else ""
        group_filter = f"*{prefix}*" if prefix else ""
        deals = mt5.history_deals_get(from_dt, to_dt, group=group_filter)
        if deals is None:
            return []

        result = []
        for d in deals:
            result.append(HistoryDeal(
                ticket=d.ticket,
                symbol=d.symbol,
                type="buy" if d.type == mt5.DEAL_TYPE_BUY else "sell",
                volume=d.volume,
                price=d.price,
                profit=d.profit,
                time=datetime.fromtimestamp(d.time, tz=timezone.utc).strftime("%Y-%m-%d %H:%M:%S") if d.time else "",
                comment=d.comment,
                magic=d.magic,
            ))
        return result

    @staticmethod
    def _pending_expiration(info, server_now: int = 0) -> tuple[int, int]:
        """Escolhe type_time/expiration aceitos pelo símbolo.

        Cada instrumento expõe em SYMBOL_EXPIRATION_MODE quais modos aceita.
        Futuros da B3 normalmente não permitem ORDER_TIME_GTC, e enviar GTC
        resulta em TRADE_RETCODE_INVALID_EXPIRATION (10022). Lemos a máscara
        do símbolo e usamos o modo permitido (GTC, dia ou data especificada),
        com a data calculada a partir do relógio do servidor.
        """
        modes = int(getattr(info, "expiration_mode", 0) or 0)
        bit_gtc = getattr(mt5, "SYMBOL_EXPIRATION_GTC", 1)
        bit_day = getattr(mt5, "SYMBOL_EXPIRATION_DAY", 2)
        bit_specified = getattr(mt5, "SYMBOL_EXPIRATION_SPECIFIED", 4)
        bit_specified_day = getattr(
            mt5, "SYMBOL_EXPIRATION_SPECIFIED_DAY", 8
        )
        order_gtc = getattr(mt5, "ORDER_TIME_GTC", 0)
        order_day = getattr(mt5, "ORDER_TIME_DAY", 1)
        order_specified = getattr(mt5, "ORDER_TIME_SPECIFIED", 2)
        order_specified_day = getattr(
            mt5, "ORDER_TIME_SPECIFIED_DAY", 3
        )

        # GTC é o padrão do MT5 e mantém o comportamento anterior; quando o
        # símbolo não o aceita (caso típico dos futuros da B3), usa "dia" e só
        # então expiração por data/hora.
        if modes & bit_gtc:
            return order_gtc, 0
        if modes & bit_day:
            return order_day, 0

        now = int(server_now or 0)
        if now <= 0:
            now = int(datetime.now(timezone.utc).timestamp())

        if modes & bit_specified_day:
            end = datetime.fromtimestamp(now, tz=timezone.utc).replace(
                hour=23, minute=59, second=59, microsecond=0
            )
            if end.timestamp() <= now + 60:
                end += timedelta(days=1)
            return order_specified_day, int(end.timestamp())

        if modes & bit_specified:
            return order_specified, now + 86400

        # Sem máscara confiável, mantém o comportamento anterior (GTC).
        return order_gtc, 0

    @staticmethod
    def _filling_mode(info) -> int:
        # B3/exchange: o símbolo só aceita FOK ou IOC; usar RETURN cai em
        # TRADE_RETCODE_INVALID_FILL (10030). Lê as flags de filling_mode.
        flags = int(getattr(info, "filling_mode", 0) or 0) if info else 0
        if flags & 1:
            return mt5.ORDER_FILLING_FOK
        if flags & 2:
            return mt5.ORDER_FILLING_IOC
        return mt5.ORDER_FILLING_RETURN

    @staticmethod
    def _is_success(retcode: int) -> bool:
        # Pending orders (limit/stop) retornam TRADE_RETCODE_PLACED (10008),
        # não DONE (10009). Sem incluir PLACED, a ordem é criada mas o app
        # reporta rejeição.
        names = (
            "TRADE_RETCODE_DONE",
            "TRADE_RETCODE_PLACED",
            "TRADE_RETCODE_DONE_PARTIAL",
        )
        codes = {getattr(mt5, name) for name in names if hasattr(mt5, name)}
        return retcode in codes

    @staticmethod
    def _retcode_name(retcode: int) -> str:
        # Monta o mapa só com as constantes existentes na versão instalada do
        # MetaTrader5. Sem isso, um retcode novo/ausente vira AttributeError e
        # derruba a resposta da ordem com HTTP 500.
        names = {
            "TRADE_RETCODE_REQUOTE": "requote",
            "TRADE_RETCODE_REJECT": "reject",
            "TRADE_RETCODE_CANCEL": "cancel",
            "TRADE_RETCODE_PLACED": "placed",
            "TRADE_RETCODE_DONE": "done",
            "TRADE_RETCODE_DONE_PARTIAL": "done_partial",
            "TRADE_RETCODE_ERROR": "error",
            "TRADE_RETCODE_TIMEOUT": "timeout",
            "TRADE_RETCODE_INVALID": "invalid",
            "TRADE_RETCODE_INVALID_VOLUME": "invalid_volume",
            "TRADE_RETCODE_INVALID_PRICE": "invalid_price",
            "TRADE_RETCODE_INVALID_STOPS": "invalid_stops",
            "TRADE_RETCODE_TRADE_DISABLED": "trade_disabled",
            "TRADE_RETCODE_MARKET_CLOSED": "market_closed",
            "TRADE_RETCODE_NO_MONEY": "no_money",
            "TRADE_RETCODE_PRICE_CHANGED": "price_changed",
            "TRADE_RETCODE_PRICE_OFF": "price_off",
            "TRADE_RETCODE_INVALID_EXPIRATION": "invalid_expiration",
            "TRADE_RETCODE_ORDER_CHANGED": "order_changed",
            "TRADE_RETCODE_TOO_MANY_REQUESTS": "too_many_requests",
            "TRADE_RETCODE_NO_CHANGES": "no_changes",
            "TRADE_RETCODE_SERVER_DISABLES_AT": "server_disables_at",
            "TRADE_RETCODE_CLIENT_DISABLES_AT": "client_disables_at",
            "TRADE_RETCODE_LOCKED": "locked",
            "TRADE_RETCODE_FROZEN": "frozen",
            "TRADE_RETCODE_INVALID_FILL": "invalid_fill",
            "TRADE_RETCODE_CONNECTION": "connection",
            "TRADE_RETCODE_ONLY_REAL": "only_real",
            "TRADE_RETCODE_LIMIT_ORDERS": "limit_orders",
            "TRADE_RETCODE_LIMIT_VOLUME": "limit_volume",
            "TRADE_RETCODE_INVALID_ORDER": "invalid_order",
            "TRADE_RETCODE_POSITION_CLOSED": "position_closed",
            "TRADE_RETCODE_INVALID_CLOSE_VOLUME": "invalid_close_volume",
            "TRADE_RETCODE_CLOSE_ORDER_EXIST": "close_order_exist",
            "TRADE_RETCODE_LIMIT_POSITIONS": "limit_positions",
            "TRADE_RETCODE_REINITIALIZE": "reinitialize",
            "TRADE_RETCODE_HEDGE_PROHIBITED": "hedge_prohibited",
        }
        mapping = {
            getattr(mt5, name): label
            for name, label in names.items()
            if hasattr(mt5, name)
        }
        return mapping.get(retcode, f"unknown_{retcode}")

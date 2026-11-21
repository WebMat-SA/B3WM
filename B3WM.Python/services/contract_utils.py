from datetime import date, datetime, timedelta
from typing import Callable, Dict, Optional

MONTH_CODES = {
    1: "F", 2: "G", 3: "H", 4: "J",
    5: "K", 6: "M", 7: "N", 8: "Q",
    9: "U", 10: "V", 11: "X", 12: "Z",
}

# Registro canônico (nome Profit) -> root do contrato no MT5.
# Adicionar um símbolo novo = uma entrada aqui (issue #47).
SYMBOL_ROOTS: Dict[str, str] = {
    "WINFUT": "WIN",
    "INDFUT": "IND",
    "WDOFUT": "WDO",
    "DOLFUT": "DOL",
}

# Regra de vencimento por root: WIN/IND bimestral (meses pares),
# WDO/DOL mensal (primeiro dia útil).
SYMBOL_FREQUENCIES: Dict[str, str] = {
    "WIN": "bimonthly",
    "IND": "bimonthly",
    "WDO": "monthly",
    "DOL": "monthly",
}


def _to_date(dt):
    if isinstance(dt, datetime):
        return dt.date()
    return dt


def _first_business_day(year: int, month: int) -> date:
    d = date(year, month, 1)
    while d.weekday() >= 5:
        d += timedelta(days=1)
    return d


def _wednesday_near_15(year: int, month: int) -> date:
    d = date(year, month, 15)
    diff = (2 - d.weekday()) % 7
    if diff > 3:
        diff -= 7
    return d + timedelta(days=diff)


def get_active_contract(ticker: str, ref_date=None) -> str:
    ticker = ticker.upper().strip()
    frequency = SYMBOL_FREQUENCIES.get(ticker)
    if frequency is None:
        raise ValueError(f"Unknown ticker: {ticker}")

    if ref_date is None:
        ref_date = date.today()
    else:
        ref_date = _to_date(ref_date)

    year = ref_date.year
    month = ref_date.month

    if frequency == "monthly":
        exp = _first_business_day(year, month)
        if ref_date > exp:
            month += 1
            if month > 12:
                month = 1
                year += 1
    else:
        if month % 2 != 0:
            month += 1
            if month > 12:
                month = 2
                year += 1
        else:
            exp = _wednesday_near_15(year, month)
            if ref_date > exp:
                month += 2
                if month > 12:
                    month = 2
                    year += 1

    return f"{ticker}{MONTH_CODES[month]}{year % 100:02d}"


def resolve_symbol(symbol: str, ref_date=None) -> str:
    """Resolve o nome canônico (ex.: INDFUT) no contrato ativo (INDM26).

    Nomes que não são canônicos e nem root conhecido (ex.: um contrato já
    específico) são devolvidos como recebidos.
    """
    if ref_date is not None:
        ref_date = _to_date(ref_date)

    base = symbol.upper().strip()
    root = SYMBOL_ROOTS.get(base)
    if root is not None:
        return get_active_contract(root, ref_date)
    if base in SYMBOL_FREQUENCIES:
        return get_active_contract(base, ref_date)
    return symbol


def get_symbol_prefix(symbol: str) -> str:
    """Root usado em filtros de posições/deals (ex.: DOLFUT -> DOL)."""
    base = symbol.upper().strip()
    root = SYMBOL_ROOTS.get(base)
    if root is not None:
        return root
    if base in SYMBOL_FREQUENCIES:
        return base
    return base


def apply_broker_affix(contract: str, prefix: str = "", suffix: str = "") -> str:
    """Aplica o prefixo/sufixo do broker sem hardcodear um único formato."""
    return f"{prefix}{contract}{suffix}"


def resolve_broker_symbol(
    symbol: str,
    ref_date=None,
    prefix: str = "",
    suffix: str = "",
    exists: Optional[Callable[[str], bool]] = None,
) -> str:
    """Resolve o nome canônico no símbolo negociável do broker.

    ``exists`` é injetável (por padrão valida via ``mt5.symbol_info``) para
    manter a função testável e permitir corretoras que exponham sufixos
    diferentes (ex.: ``IND``, ``IND$``, ``IND@N``).
    """
    contract = resolve_symbol(symbol, ref_date)
    candidate = apply_broker_affix(contract, prefix, suffix)

    if exists is not None and exists(candidate):
        return candidate

    # Fallback: tenta o contrato sem afixo (brokers que usam o root puro).
    if candidate != contract and (exists is None or exists(contract)):
        return contract

    return candidate

from datetime import date, datetime, timedelta
from typing import Optional

MONTH_CODES = {
    1: "F", 2: "G", 3: "H", 4: "J",
    5: "K", 6: "M", 7: "N", 8: "Q",
    9: "U", 10: "V", 11: "X", 12: "Z",
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
    if ref_date is None:
        ref_date = date.today()
    else:
        ref_date = _to_date(ref_date)

    year = ref_date.year
    month = ref_date.month

    if ticker == "WDO":
        exp = _first_business_day(year, month)
        if ref_date > exp:
            month += 1
            if month > 12:
                month = 1
                year += 1
    elif ticker == "WIN":
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
    else:
        raise ValueError(f"Unknown ticker: {ticker}")

    return f"{ticker}{MONTH_CODES[month]}{year % 100:02d}"


def resolve_symbol(symbol: str, ref_date=None) -> str:
    if ref_date is not None:
        ref_date = _to_date(ref_date)
    base = symbol.upper().strip()
    if base == "WDOFUT":
        return get_active_contract("WDO", ref_date)
    if base == "WINFUT":
        return get_active_contract("WIN", ref_date)
    return symbol

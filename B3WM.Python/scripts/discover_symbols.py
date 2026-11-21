"""Descoberta dos símbolos dos contratos cheios no MT5 (issue #47).

Rode no Windows, com o terminal MT5 da corretora aberto e logado:

    python scripts/discover_symbols.py

O objetivo é confirmar o nome exato de IND/DOL (prefixo/sufixo do broker),
o tick, o valor do tick e a rolagem do contrato antes de fixar o mapeamento.
"""

import os
import sys

try:
    import MetaTrader5 as mt5
except ImportError:
    print("MetaTrader5 não instalado. Rode: pip install MetaTrader5", file=sys.stderr)
    raise SystemExit(1)

sys.path.insert(
    0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from services.contract_utils import resolve_broker_symbol, resolve_symbol  # noqa: E402

CANONICAL = ["WINFUT", "INDFUT", "WDOFUT", "DOLFUT"]


def dump(pattern: str) -> None:
    print(f"\n=== {pattern} ===")
    symbols = mt5.symbols_get(pattern)
    if not symbols:
        print("(nenhum símbolo encontrado)")
        return

    for s in symbols:
        print(
            f"{s.name} | {s.description} | "
            f"tick={s.trade_tick_size} | tick_value={s.trade_tick_value} | "
            f"contrato={s.trade_contract_size}"
        )


def resolve_canonical(prefix: str, suffix: str) -> None:
    print("\n=== rolagem dos nomes canônicos ===")
    for canonical in CANONICAL:
        contract = resolve_symbol(canonical)
        resolved = resolve_broker_symbol(
            canonical,
            prefix=prefix,
            suffix=suffix,
            exists=lambda s: mt5.symbol_info(s) is not None,
        )
        info = mt5.symbol_info(resolved)
        if info is None:
            print(f"{canonical} -> {contract} | resolvido={resolved} | NÃO EXISTE no MT5")
            continue

        print(
            f"{canonical} -> {contract} | resolvido={resolved} | "
            f"tick={info.trade_tick_size} | tick_value={info.trade_tick_value} | "
            f"contrato={info.trade_contract_size}"
        )


def main() -> None:
    if not mt5.initialize():
        print(f"mt5.initialize() falhou: {mt5.last_error()}", file=sys.stderr)
        raise SystemExit(1)

    prefix = os.getenv("MT5_SYMBOL_PREFIX", "")
    suffix = os.getenv("MT5_SYMBOL_SUFFIX", "")

    try:
        dump("*IND*")
        dump("*DOL*")
        resolve_canonical(prefix, suffix)
    finally:
        mt5.shutdown()


if __name__ == "__main__":
    main()

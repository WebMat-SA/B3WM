import os
import sys
import unittest
from datetime import date

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from services.contract_utils import (  # noqa: E402
    apply_broker_affix,
    get_active_contract,
    get_symbol_prefix,
    resolve_broker_symbol,
    resolve_symbol,
)


class ContractUtilsTests(unittest.TestCase):
    def test_index_contract_is_bimonthly(self):
        # Maio (ímpar) rola para o bimestre seguinte (junho).
        self.assertEqual(get_active_contract("IND", date(2026, 5, 10)), "INDM26")
        # Antes do vencimento (quarta 17/06) segue no mesmo contrato.
        self.assertEqual(get_active_contract("IND", date(2026, 6, 17)), "INDM26")
        # Depois do vencimento rola dois meses (agosto).
        self.assertEqual(get_active_contract("IND", date(2026, 6, 18)), "INDQ26")

    def test_dollar_contract_is_monthly(self):
        # 01/05/2026 é o primeiro dia útil de maio.
        self.assertEqual(get_active_contract("DOL", date(2026, 5, 1)), "DOLK26")
        self.assertEqual(get_active_contract("DOL", date(2026, 5, 2)), "DOLM26")

    def test_win_and_wdo_keep_legacy_rules(self):
        self.assertEqual(get_active_contract("WIN", date(2026, 6, 10)), "WINM26")
        self.assertEqual(get_active_contract("WDO", date(2026, 5, 2)), "WDOM26")

    def test_resolve_symbol_maps_canonical_names(self):
        self.assertEqual(resolve_symbol("INDFUT", date(2026, 6, 10)), "INDM26")
        self.assertEqual(resolve_symbol("DOLFUT", date(2026, 5, 2)), "DOLM26")
        self.assertEqual(resolve_symbol("WINFUT", date(2026, 6, 10)), "WINM26")
        self.assertEqual(resolve_symbol("WDOFUT", date(2026, 5, 2)), "WDOM26")
        # Root/contrato já resolvido passa direto.
        self.assertEqual(resolve_symbol("INDM26"), "INDM26")
        self.assertEqual(resolve_symbol("IND", date(2026, 6, 10)), "INDM26")

    def test_symbol_prefix_maps_to_mt5_root(self):
        self.assertEqual(get_symbol_prefix("INDFUT"), "IND")
        self.assertEqual(get_symbol_prefix("DOLFUT"), "DOL")
        self.assertEqual(get_symbol_prefix("WINFUT"), "WIN")
        self.assertEqual(get_symbol_prefix("WDOFUT"), "WDO")
        self.assertEqual(get_symbol_prefix("INDM26"), "INDM26")

    def test_broker_affix_is_configurable(self):
        self.assertEqual(apply_broker_affix("INDM26"), "INDM26")
        self.assertEqual(apply_broker_affix("INDM26", suffix="$"), "INDM26$")
        self.assertEqual(apply_broker_affix("INDM26", prefix="X"), "XINDM26")

    def test_resolve_broker_symbol_prefers_existing_candidate(self):
        resolved = resolve_broker_symbol(
            "INDFUT",
            date(2026, 6, 10),
            suffix="$",
            exists=lambda s: s == "INDM26$",
        )
        self.assertEqual(resolved, "INDM26$")

    def test_resolve_broker_symbol_falls_back_to_plain_contract(self):
        resolved = resolve_broker_symbol(
            "INDFUT",
            date(2026, 6, 10),
            suffix="$",
            exists=lambda s: s == "INDM26",
        )
        self.assertEqual(resolved, "INDM26")

    def test_unknown_ticker_raises(self):
        with self.assertRaises(ValueError):
            get_active_contract("PETR", date(2026, 5, 1))


if __name__ == "__main__":
    unittest.main()

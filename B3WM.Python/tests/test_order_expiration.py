import importlib.util
import os
import sys
import types
import unittest
from datetime import datetime, timezone
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

if importlib.util.find_spec("pydantic") is None:
    # Ambiente de teste sem as dependências do bridge: os modelos Pydantic
    # não são instanciados, só precisam existir para o import do executor.
    _fake_pydantic = types.ModuleType("pydantic")
    _fake_pydantic.BaseModel = type("BaseModel", (), {})
    sys.modules.setdefault("pydantic", _fake_pydantic)

# O bridge só instala MetaTrader5 no Windows; para testar a seleção de
# expiração basta um stub com as mesmas constantes da API.
_fake_mt5 = types.ModuleType("MetaTrader5")
_fake_mt5.SYMBOL_EXPIRATION_GTC = 1
_fake_mt5.SYMBOL_EXPIRATION_DAY = 2
_fake_mt5.SYMBOL_EXPIRATION_SPECIFIED = 4
_fake_mt5.SYMBOL_EXPIRATION_SPECIFIED_DAY = 8
_fake_mt5.ORDER_TIME_GTC = 0
_fake_mt5.ORDER_TIME_DAY = 1
_fake_mt5.ORDER_TIME_SPECIFIED = 2
_fake_mt5.ORDER_TIME_SPECIFIED_DAY = 3
sys.modules.setdefault("MetaTrader5", _fake_mt5)

import MetaTrader5 as mt5  # noqa: E402

from services.order_executor import MT5OrderExecutor  # noqa: E402


class PendingExpirationTests(unittest.TestCase):
    def _info(self, modes):
        return SimpleNamespace(expiration_mode=modes)

    def test_b3_day_only_avoids_gtc(self):
        # Futuros da B3 normalmente só aceitam "Dia"; GTC gera 10022.
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(mt5.SYMBOL_EXPIRATION_DAY)
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_DAY)
        self.assertEqual(expiration, 0)

    def test_gtc_has_priority_when_both_allowed(self):
        # Com GTC permitido mantém o padrão do MT5 (e o comportamento antigo).
        modes = mt5.SYMBOL_EXPIRATION_GTC | mt5.SYMBOL_EXPIRATION_DAY
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(modes)
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_GTC)
        self.assertEqual(expiration, 0)

    def test_gtc_only_is_preserved(self):
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(mt5.SYMBOL_EXPIRATION_GTC)
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_GTC)
        self.assertEqual(expiration, 0)

    def test_specified_day_gets_future_expiration(self):
        now = 1_700_000_000
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(mt5.SYMBOL_EXPIRATION_SPECIFIED_DAY), now
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_SPECIFIED_DAY)
        self.assertGreater(expiration, now)
        end = datetime.fromtimestamp(expiration, tz=timezone.utc)
        self.assertEqual((end.hour, end.minute, end.second), (23, 59, 59))

    def test_specified_gets_future_expiration(self):
        now = 1_700_000_000
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(mt5.SYMBOL_EXPIRATION_SPECIFIED), now
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_SPECIFIED)
        self.assertGreater(expiration, now)

    def test_unknown_mask_falls_back_to_gtc(self):
        type_time, expiration = MT5OrderExecutor._pending_expiration(
            self._info(0)
        )
        self.assertEqual(type_time, mt5.ORDER_TIME_GTC)
        self.assertEqual(expiration, 0)


if __name__ == "__main__":
    unittest.main()

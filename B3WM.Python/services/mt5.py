import asyncio
from datetime import datetime, timedelta
from typing import Callable, Optional

import MetaTrader5 as mt5
import pandas as pd

TickCallback = Optional[Callable[[pd.DataFrame], None]]

# Global list to accumulate all collected ticks
all_ticks = []


class MT5TickCollector:
    def __init__(
        self,
        symbol: str,
        utc_from: datetime,
        max_ticks: int = 1000,
        on_ticks: TickCallback = None,
        interval: float = 1.0,
    ):
        self.symbol = symbol
        self.utc_from = utc_from
        self.max_ticks = max_ticks
        self.on_ticks = on_ticks
        self.interval = interval
        self._connected = False

    def connect(self) -> bool:
        if not mt5.initialize():
            print("initialize() failed, error code =", mt5.last_error())
            return False

        self._connected = True
        return True

    def shutdown(self) -> None:
        if self._connected:
            mt5.shutdown()
            self._connected = False

    async def run(self) -> None:
        if not self.connect():
            return

        try:
            while True:
                ticks = mt5.copy_ticks_from(
                    self.symbol,
                    self.utc_from,
                    self.max_ticks,
                    mt5.COPY_TICKS_ALL,
                )

                # quero que todos os ticks sejam impressos
                for tick in ticks:
                    #como não sei do que o tick é formado, é possivel transformar a info em texto para imprimir
                    print(tick)


                if ticks is None:
                    print("Failed to get ticks for", self.symbol, "error code =", mt5.last_error())
                else:
                    ticks_df = pd.DataFrame(ticks)
                    if not ticks_df.empty:
                        ticks_df["time"] = pd.to_datetime(ticks_df["time"], unit="s")
                        #if self.on_ticks:
                            #self.on_ticks(ticks_df)

                        last_time = ticks_df["time"].max()
                        self.utc_from = last_time + timedelta(milliseconds=1)

                await asyncio.sleep(self.interval)
        finally:
            self.shutdown()


def default_tick_handler(ticks_df: pd.DataFrame) -> None:
    global all_ticks
    all_ticks.extend(ticks_df.to_dict('records'))
    print(f"Collected {len(ticks_df)} new ticks. Total collected: {len(all_ticks)}")
    for tick in ticks_df.itertuples():
        print(f"Time: {tick.time}, Price: {tick.price}, Volume: {tick.volume}")


if __name__ == "__main__":
    collector = MT5TickCollector(
        symbol="WINM26",
        utc_from=datetime(2026, 5, 8, 12, 0, 0),
        max_ticks=1000,
        on_ticks=default_tick_handler,
        interval=1.0,
    )

    asyncio.run(collector.run())

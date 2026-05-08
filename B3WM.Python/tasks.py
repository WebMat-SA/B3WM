import asyncio
from typing import Callable, Optional

from safedict import SafeDict

OnValueCallback = Optional[Callable[[str], None]]

class AdderTask:
    def __init__(self, storage: SafeDict, on_add: OnValueCallback = None, interval: float = 1.0):
        self.storage = storage
        self.on_add = on_add
        self.interval = interval
        self.counter = 0

    async def run(self):
        while True:
            value = f"value{self.counter}"
            self.storage[f"key{self.counter}"] = value
            if self.on_add:
                self.on_add(value)
            self.counter += 1
            await asyncio.sleep(self.interval)

class PrinterTask:
    def __init__(self, storage: SafeDict, on_print: OnValueCallback = None, interval: float = 1.5):
        self.storage = storage
        self.on_print = on_print
        self.interval = interval

    async def run(self):
        while True:
            if self.storage:
                numeric_keys = [k for k in self.storage.keys() if k.startswith('key') and k[3:].isdigit()]
                if numeric_keys:
                    last_key = max(numeric_keys, key=lambda k: int(k[3:]))
                    last_value = self.storage[last_key]
                    print(f"Último valor: {last_value}")
                    if self.on_print:
                        self.on_print(last_value)
            await asyncio.sleep(self.interval)

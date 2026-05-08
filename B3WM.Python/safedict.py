from collections import UserDict
from threading import Lock

class SafeDict(UserDict):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._lock = Lock()

    def __setitem__(self, key, value):
        with self._lock:
            super().__setitem__(key, value)

    def __getitem__(self, key):
        with self._lock:
            return super().__getitem__(key)

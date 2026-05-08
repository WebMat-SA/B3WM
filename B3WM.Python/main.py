from safedict import SafeDict
import asyncio

from tasks import AdderTask, PrinterTask

safedict = SafeDict()

def handle_add(value):
    #print(f"Callback: Valor adicionado - {value}")
    print_safedict()


def handle_print(value):
    print(f"Callback: Valor impresso - {value}")

def print_safedict():
    print("Conteúdo atual do SafeDict:")
    for key, value in safedict.items():
        print(f"{key}: {value}")

async def main():
    print("Hello from B3WM.Python")
    safedict["key1"] = "value1"
    print(safedict["key1"])

    adder = AdderTask(storage=safedict, on_add=handle_add, interval=1.0)
    # printer = PrinterTask(storage=safedict, on_print=handle_print, interval=1.5)

    await asyncio.gather(
        adder.run(),
        # printer.run()
    )


if __name__ == "__main__":
    asyncio.run(main())
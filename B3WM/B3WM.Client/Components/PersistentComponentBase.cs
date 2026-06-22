using Microsoft.AspNetCore.Components;

namespace B3WM.Client.Components
{
    public abstract class PersistentComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject] protected ComponentStateService StateService { get; set; } = default!;
        protected abstract string PersistenceKey { get; }

        // 🔹 Permite override opcional
        protected virtual bool ContinuousSave => false;
        protected virtual TimeSpan PersistenceInterval => TimeSpan.FromSeconds(15);

        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;

        private volatile bool _dirty;
        private string? _lastSnapshot;

        protected override async Task OnInitializedAsync()
        {
            //Load
            await ForceReload();

            // Inicia loop
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(PersistenceInterval);

            _ = RunPersistenceLoop(_cts.Token);
        }

        protected void MarkDirty()
        {
            _dirty = true;
        }

        public async Task ForceReload()
        {
            // Restaura estado salvo
            await StateService.RestoreAsync(this, PersistenceKey);

            // Cria snapshot inicial
            _lastSnapshot = StateService.CreateSnapshot(this);
        }

        private async Task RunPersistenceLoop(CancellationToken token)
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(token))
                {
                    if (!_dirty && !ContinuousSave)
                        continue;

                    await PersistIfChangedAsync();
                    _dirty = false;
                }
            }
            catch (OperationCanceledException)
            {
                // esperado no dispose
            }
        }

        private async Task PersistIfChangedAsync()
        {
            var currentSnapshot = StateService.CreateSnapshot(this);

            if (currentSnapshot == _lastSnapshot)
                return;

            _lastSnapshot = currentSnapshot;
            await StateService.SaveAsync(this, PersistenceKey);
        }

        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();

            // Flush final se estiver dirty
            if (_dirty)
                await PersistIfChangedAsync();

            _timer?.Dispose();
            _cts?.Dispose();
        }
    }
}

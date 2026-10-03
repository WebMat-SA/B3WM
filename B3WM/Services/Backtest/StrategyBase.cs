using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    /// <summary>
    /// Base com o tracker de estrutura (mesma lógica do StructureService).
    /// Reset() limpa só estado transitório (bordas/flags/linhas) e preserva
    /// os dados carregados (_savedStructure), permitindo re-run sem recarregar disco.
    /// </summary>
    public abstract class StrategyBase : IStrategy, IStructureProvider
    {
        protected readonly BacktestConfig Config;
        protected readonly Func<DateTime, StructureStorageItem?>? StructureProvider;
        protected readonly Dictionary<DateTime, StructureStorageItem> SavedStructure = new();
        private readonly List<StructureStorageItem> _structureLines = new();

        protected double? UpBorder, DownBorder;
        protected double UpAuxBorder, DownAuxBorder;
        protected bool ExpectBuyDrop = true, ExpectSellDrop = true, IsSizeChanger;
        protected readonly double MinDistance;
        protected bool Initialized;

        public IReadOnlyList<StructureStorageItem> StructureLines => _structureLines;

        public abstract string Name { get; }
        public virtual string Description => Name;
        public virtual int WarmupBars => 1;

        protected StrategyBase(
            BacktestConfig config,
            Func<DateTime, StructureStorageItem?>? structureProvider = null)
        {
            Config = config;
            StructureProvider = structureProvider;
            MinDistance = config.MinDistance > 0 ? config.MinDistance : Defaults.GetMinDistance(config.Symbol);
        }

        public virtual Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public abstract EntrySignal? TryGetEntry(BarStorageItem bar);
        public abstract ExitSignal? TryGetExit(BarStorageItem bar, BacktestPosition position);

        protected async Task LoadSavedStructureAsync(DataKeeperBase? dataKeeper, CancellationToken ct)
        {
            if (dataKeeper == null || Initialized) return;
            var current = Config.StartDate.Date;
            while (current <= Config.EndDate.Date)
            {
                ct.ThrowIfCancellationRequested();
                var path = $"{Config.Symbol}_{nameof(StructureService)}_{Config.TimeFrame}MIN_{MinDistance}_{current:yyyy-MM-dd}.json";
                try
                {
                    var structure = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                    if (structure != null)
                    {
                        foreach (var s in structure)
                        {
                            if (!SavedStructure.ContainsKey(s.Date))
                                SavedStructure[s.Date] = s;
                        }
                    }
                }
                catch (Exception)
                {
                    // dia sem estrutura salva
                }
                current = current.AddDays(1);
            }
            Initialized = true;
        }

        protected StructureStorageItem? ResolveStructure(BarStorageItem bar)
        {
            if (StructureProvider != null)
            {
                var p = StructureProvider(bar.Date);
                if (p != null)
                {
                    SyncBorders(p);
                    return p;
                }
            }

            if (SavedStructure.TryGetValue(bar.Date, out var saved))
            {
                SyncBorders(saved);
                return saved;
            }

            return UpdateStructure(bar);
        }

        protected void SyncBorders(StructureStorageItem s)
        {
            UpBorder = s.UpBorder;
            DownBorder = s.DownBorder;
            UpAuxBorder = s.UpAuxBorder;
            DownAuxBorder = s.DownAuxBorder;
            TrackStructure(s);
        }

        protected StructureStorageItem UpdateStructure(BarStorageItem bar)
        {
            if (UpBorder == null)
            {
                UpBorder = bar.High;
                DownBorder = bar.Low;
                UpAuxBorder = bar.High;
                DownAuxBorder = bar.Low;
            }
            else
            {
                var virtualUpAux = Math.Max(UpAuxBorder, bar.High);
                var virtualDownAux = Math.Min(DownAuxBorder, bar.Low);

                if (virtualUpAux - bar.Close >= MinDistance && ExpectBuyDrop)
                {
                    UpAuxBorder = virtualUpAux;
                    UpBorder = UpAuxBorder;
                    ExpectSellDrop = true;
                    ExpectBuyDrop = false;
                    DownAuxBorder = bar.Low;
                    IsSizeChanger = true;
                }

                if (bar.Close - virtualDownAux >= MinDistance && ExpectSellDrop && !IsSizeChanger)
                {
                    DownAuxBorder = virtualDownAux;
                    DownBorder = DownAuxBorder;
                    ExpectBuyDrop = true;
                    ExpectSellDrop = false;
                    UpAuxBorder = bar.High;
                }

                UpAuxBorder = Math.Max(UpAuxBorder, bar.High);
                DownAuxBorder = Math.Min(DownAuxBorder, bar.Low);
                IsSizeChanger = false;
            }

            var item = new StructureStorageItem
            {
                Date = bar.Date,
                Symbol = Config.Symbol,
                TimeFrame = Config.TimeFrame,
                UpBorder = UpBorder ?? double.NaN,
                DownBorder = DownBorder ?? double.NaN,
                UpAuxBorder = UpAuxBorder,
                DownAuxBorder = DownAuxBorder
            };

            TrackStructure(item);
            return item;
        }

        protected void TrackStructure(StructureStorageItem s)
        {
            if (s.Clone() is StructureStorageItem clone)
                _structureLines.Add(clone);
        }

        public virtual void Reset()
        {
            UpBorder = DownBorder = null;
            UpAuxBorder = DownAuxBorder = 0;
            ExpectBuyDrop = ExpectSellDrop = true;
            IsSizeChanger = false;
            _structureLines.Clear();
            // NÃO limpa SavedStructure: permite re-run após Reset sem recarregar.
        }
    }
}

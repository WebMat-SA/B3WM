using B3WM.Shared.Models;

namespace B3WM.Services.Core
{
    /// <summary>
    /// Estado do detector de estruturas: bordas efetivas (up/down), bordas
    /// auxiliares (extremos da perna) e as flags de expectativa de retrocesso.
    /// </summary>
    public sealed class StructureEngineState
    {
        public double UpBorder { get; set; }
        public double DownBorder { get; set; }
        public double UpAuxBorder { get; set; }
        public double DownAuxBorder { get; set; }

        /// <summary>Espera um retrocesso a partir do topo para confirmar a borda superior.</summary>
        public bool ExpectBuyDrop { get; set; } = true;

        /// <summary>Espera um retrocesso a partir do fundo para confirmar a borda inferior.</summary>
        public bool ExpectSellDrop { get; set; } = true;

        public StructureEngineState Clone() =>
            (StructureEngineState)MemberwiseClone();

        /// <summary>
        /// Semeia o estado a partir de uma estrutura persistida. As flags não
        /// são persistidas no arquivo (mesmo comportamento do serviço antigo),
        /// então voltam ao default true/true.
        /// </summary>
        public static StructureEngineState FromStructure(StructureStorageItem item) => new()
        {
            UpBorder = item.UpBorder,
            DownBorder = item.DownBorder,
            UpAuxBorder = item.UpAuxBorder,
            DownAuxBorder = item.DownAuxBorder,
            ExpectBuyDrop = true,
            ExpectSellDrop = true,
        };
    }

    /// <summary>Resultado de um passo do engine.</summary>
    public readonly record struct StructureStepResult(
        bool Initialized,
        bool UpChanged,
        bool DownChanged,
        double RangeUsed)
    {
        public bool Changed => UpChanged || DownChanged;
    }

    /// <summary>
    /// Núcleo puro do cálculo de estrutura, extraído de
    /// <c>StructureService.Generate</c> sem alterar a regra. Recebe o range
    /// efetivo por parâmetro para permitir range fixo, ATR por perna ou ATR
    /// por barra sem duplicar a lógica.
    /// </summary>
    public sealed class StructureEngine
    {
        public StructureEngine(StructureEngineState? state = null)
        {
            State = state?.Clone();
        }

        public StructureEngineState? State { get; private set; }

        public bool IsInitialized => State != null;

        /// <summary>
        /// Processa um candle fechado. No primeiro candle apenas inicializa as
        /// bordas com high/low; não conta como mudança.
        /// </summary>
        public StructureStepResult Step(BarStorageItem bar, double range)
        {
            if (State == null)
            {
                State = new StructureEngineState
                {
                    UpBorder = bar.High,
                    DownBorder = bar.Low,
                    UpAuxBorder = bar.High,
                    DownAuxBorder = bar.Low,
                };

                return new StructureStepResult(true, false, false, range);
            }

            var state = State;
            var upChanged = false;
            var downChanged = false;

            var virtualSameUpAuxBorder = Math.Max(state.UpAuxBorder, bar.High);
            var virtualSameDownAuxBorder = Math.Min(state.DownAuxBorder, bar.Low);
            var isSizeChanger = false;

            // Distanciamento do topo (retrocesso para baixo): confirma a borda
            // superior no extremo e reseta a perna.
            if (virtualSameUpAuxBorder - bar.Close >= range && state.ExpectBuyDrop)
            {
                state.UpAuxBorder = virtualSameUpAuxBorder;
                state.UpBorder = state.UpAuxBorder;
                state.ExpectSellDrop = true;
                state.ExpectBuyDrop = false;
                state.DownAuxBorder = bar.Low;
                isSizeChanger = true;
                upChanged = true;
            }

            // Distanciamento do fundo (retrocesso para cima): confirma a borda
            // inferior. O !isSizeChanger evita as duas mudanças no mesmo candle.
            if (bar.Close - virtualSameDownAuxBorder >= range &&
                state.ExpectSellDrop &&
                !isSizeChanger)
            {
                state.DownAuxBorder = virtualSameDownAuxBorder;
                state.DownBorder = state.DownAuxBorder;
                state.ExpectSellDrop = false;
                state.ExpectBuyDrop = true;
                state.UpAuxBorder = bar.High;
                downChanged = true;
            }

            state.UpAuxBorder = Math.Max(state.UpAuxBorder, bar.High);
            state.DownAuxBorder = Math.Min(state.DownAuxBorder, bar.Low);

            return new StructureStepResult(false, upChanged, downChanged, range);
        }

        public StructureStorageItem ToStorage(string symbol, DateTime date, int timeFrame)
        {
            var state = State ?? new StructureEngineState();
            return new StructureStorageItem
            {
                Symbol = symbol,
                Date = date,
                TimeFrame = timeFrame,
                UpBorder = state.UpBorder,
                DownBorder = state.DownBorder,
                UpAuxBorder = state.UpAuxBorder,
                DownAuxBorder = state.DownAuxBorder,
            };
        }
    }
}

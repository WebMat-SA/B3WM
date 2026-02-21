namespace B3WM.Client.Services
{
    /// <summary>
    /// Configuração central para log de performance dos Helpers.
    /// Ative a flag do serviço desejado para ver métricas no console.
    /// </summary>
    public static class HelperPerformanceConfig
    {
        /// <summary>Quando true, CandleHelper imprime [Perf] no console.</summary>
        public static bool EnableCandleHelper { get; set; } = false;

        /// <summary>Quando true, VolumeHelper imprime [Perf] no console.</summary>
        public static bool EnableVolumeHelper { get; set; } = false;

        /// <summary>Quando true, BubbleHelper imprime [Perf] no console.</summary>
        public static bool EnableBubbleHelper { get; set; } = false;

        /// <summary>Quando true, DataHelper imprime [Perf] no console.</summary>
        public static bool EnableDataHelper { get; set; } = false;

        /// <summary>Quando true, IndexedDbStorageAccessor imprime [Perf] no console.</summary>
        public static bool EnableIndexedDbStorage { get; set; } = false;

        /// <summary>
        /// Loga sempre quando a operação ultrapassa este tempo (ms).
        /// </summary>
        public static int SlowOperationMs { get; set; } = 120;

        /// <summary>
        /// Além dos lentos, também loga 1 em cada N operações (amostragem).
        /// Use 1 para logar tudo.
        /// </summary>
        public static int SampleEveryOperations { get; set; } = 20;

        internal static void Log(string helperName, string operation, long elapsedMs, string? extra = null)
        {
            if (!IsEnabled(helperName)) return;

            var msg = extra != null
                ? $"[Perf] {helperName}.{operation} = {elapsedMs} ms | {extra}"
                : $"[Perf] {helperName}.{operation} = {elapsedMs} ms";
            Console.WriteLine(msg);
        }

        internal static void LogSampled(string helperName, string operation, long elapsedMs, int sequence, string? extra = null)
        {
            if (!IsEnabled(helperName)) return;

            var sampleEvery = SampleEveryOperations <= 0 ? 1 : SampleEveryOperations;
            var isSlow = elapsedMs >= SlowOperationMs;
            var isSampleHit = sequence <= 1 || (sequence % sampleEvery == 0);
            if (!isSlow && !isSampleHit) return;

            Log(helperName, operation, elapsedMs, $"seq={sequence}" + (string.IsNullOrWhiteSpace(extra) ? string.Empty : $" | {extra}"));
        }

        private static bool IsEnabled(string helperName) => helperName switch
        {
            nameof(CandleHelper) => EnableCandleHelper,
            nameof(VolumeHelper) => EnableVolumeHelper,
            nameof(BubbleHelper) => EnableBubbleHelper,
            nameof(DataHelper) => EnableDataHelper,
            nameof(IndexedDbStorageAccessor) => EnableIndexedDbStorage,
            _ => false
        };
    }
}

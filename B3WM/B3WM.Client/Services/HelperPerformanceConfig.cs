using B3WM.Client.Components;
using B3WM.Client.Pages;

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

        /// <summary>Quando true, MapFlow imprime [Perf] no console.</summary>
        public static bool EnableMapFlow { get; set; } = false;

        /// <summary>Quando true, MapFlowGraph imprime [Perf] no console.</summary>
        public static bool EnableMapFlowGraph { get; set; } = false;

        /// <summary>Quando true, HubClient imprime [Perf] no console.</summary>
        public static bool EnableHubClient { get; set; } = true;

        /// <summary>Quando true, MainHelper imprime [Perf] no console.</summary>
        public static bool EnableMainHelper { get; set; } = false;

        internal static void Log(string helperName, string operation, long elapsedMs, string? extra = null)
        {
            if (!IsEnabled(helperName)) return;

            var msg = extra != null
                ? $"[Perf] {helperName}.{operation} = {elapsedMs} ms | {extra}"
                : $"[Perf] {helperName}.{operation} = {elapsedMs} ms";
            Console.WriteLine(msg);
        }

        private static bool IsEnabled(string helperName) => helperName switch
        {
            nameof(CandleHelper) => EnableCandleHelper,
            nameof(VolumeHelper) => EnableVolumeHelper,
            nameof(BubbleHelper) => EnableBubbleHelper,
            nameof(DataHelper) => EnableDataHelper,
            nameof(IndexedDbStorageAccessor) => EnableIndexedDbStorage,
            nameof(MapFlow) => EnableMapFlow,
            nameof(MapFlowGraph) => EnableMapFlowGraph,
            nameof(HubClient) => EnableHubClient,
            nameof(MainHelper) => EnableMainHelper,
            _ => false
        };
    }
}

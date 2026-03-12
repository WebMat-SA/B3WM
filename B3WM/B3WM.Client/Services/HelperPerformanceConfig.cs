using B3WM.Client.Components;
using B3WM.Client.Pages;
using Magic.IndexedDb;

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

        /// <summary>Quando true, MapFlow imprime [Perf] no console.</summary>
        public static bool EnableMapFlow { get; set; } = false;

        /// <summary>Quando true, HubClient imprime [Perf] no console.</summary>
        public static bool EnableHubClient { get; set; } = false;

        /// <summary> Quando true, HubClientWorker imprime [Perf] no console. </summary>
        public static bool EnableHubClientWorker { get; set; } = true;

        /// <summary>Quando true, MainHelper imprime [Perf] no console.</summary>
        public static bool EnableMainHelper { get; set; } = false;

        /// <summary>Quando true, DbMagic imprime [Perf] no console.</summary>
        public static bool EnableMagicDb { get; set; } = false;

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
            nameof(MapFlow) => EnableMapFlow,
            nameof(HubClient) => EnableHubClient,
            nameof(MainHelper) => EnableMainHelper,
            nameof(HubClientWorker) => EnableHubClientWorker,
            nameof(Magic) => EnableMagicDb,
            "Log" => true,
            _ => false
        };
    }
}

namespace B3WM.Shared.Models.Dashboard
{
    /// <summary>
    /// Estados padronizados exibidos no dashboard. São strings (e não enum)
    /// para manter o contrato JSON simples e estável para o JS.
    /// </summary>
    public static class DashboardStatus
    {
        public const string Ok = "ok";
        public const string Warn = "warn";
        public const string Error = "error";
        public const string Idle = "idle";
    }

    /// <summary>Snapshot completo consumido pelo dashboard a cada polling.</summary>
    public sealed class DashboardHealth
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime ServerTime { get; set; }
        public long UptimeSeconds { get; set; }
        public string Environment { get; set; } = "";
        public string Version { get; set; } = "";
        public string Overall { get; set; } = DashboardStatus.Idle;
        public DashboardDataDirectory DataDirectory { get; set; } = new();
        public List<DashboardSymbolHealth> Symbols { get; set; } = new();
        public DashboardIntegrations Integrations { get; set; } = new();
        public List<DashboardUnknownAgent> UnknownAgents { get; set; } = new();
        public List<DashboardRecentError> RecentErrors { get; set; } = new();
        public DashboardConnections Connections { get; set; } = new();
    }

    public sealed class DashboardDataDirectory
    {
        public string Path { get; set; } = "";
        public bool Exists { get; set; }
        public bool Writable { get; set; }
        public DateTime? LastWriteAt { get; set; }
    }

    public sealed class DashboardConnections
    {
        public int ConnectedClients { get; set; }
        public long TotalConnections { get; set; }
    }

    public sealed class DashboardSymbolHealth
    {
        public string Symbol { get; set; } = "";
        public string Status { get; set; } = DashboardStatus.Idle;
        public DashboardDataSourceHealth DataSource { get; set; } = new();
        public List<DashboardCandleHealth> Candles { get; set; } = new();
        public DashboardBubbleHealth Bubble { get; set; } = new();
        public DashboardVolumeHealth Volume { get; set; } = new();
        public List<DashboardStructureHealth> Structure { get; set; } = new();
        public DashboardValueHealth Extreme { get; set; } = new();
        public DashboardValueHealth Vwap { get; set; } = new();
        public DashboardOrchestrationHealth Orchestration { get; set; } = new();
        public DashboardStrategyHealth Strategies { get; set; } = new();
    }

    public sealed class DashboardDataSourceHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public DateTime? LastTickAt { get; set; }
        public double? AgeSeconds { get; set; }
        public long TicksLastMinute { get; set; }
        public long TotalTicks { get; set; }
        public DateTime? FirstTickAt { get; set; }
        public DateTime? LastUnknownAgentAt { get; set; }
    }

    public sealed class DashboardCandleHealth
    {
        public int TimeFrame { get; set; }
        public string Status { get; set; } = DashboardStatus.Idle;
        public DateTime? LastBarAt { get; set; }
        public double? AgeSeconds { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public long Volume { get; set; }
        public bool IsToday { get; set; }
    }

    public sealed class DashboardBubbleHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public DateTime? LastAt { get; set; }
        public double? AgeSeconds { get; set; }
        public int? Agent { get; set; }
        public string AgentName { get; set; } = "";
        public decimal? Amount { get; set; }
        public double? Price { get; set; }
        public string ActionType { get; set; } = "";
    }

    public sealed class DashboardVolumeHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public DateTime? LastAt { get; set; }
        public double? AgeSeconds { get; set; }
        public int Levels { get; set; }
        public long TotalVolume { get; set; }
    }

    public sealed class DashboardStructureHealth
    {
        public int TimeFrame { get; set; }
        public string Status { get; set; } = DashboardStatus.Idle;
        /// <summary>Range em uso agora pelo StructureService.</summary>
        public double Range { get; set; }
        /// <summary>Range deduzido dos arquivos persistidos no startup.</summary>
        public double? ResolvedRange { get; set; }
        /// <summary>Range default configurado para o símbolo/TF.</summary>
        public double DefaultRange { get; set; }
        public DateTime? LastAt { get; set; }
        public double? AgeSeconds { get; set; }
        public double? UpBorder { get; set; }
        public double? DownBorder { get; set; }
        public double? UpAuxBorder { get; set; }
        public double? DownAuxBorder { get; set; }
        public bool IsToday { get; set; }
    }

    /// <summary>Serviços cujo snapshot é essencialmente um valor/horário (VWAP, extremes).</summary>
    public sealed class DashboardValueHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public DateTime? LastAt { get; set; }
        public double? AgeSeconds { get; set; }
        public double? Value { get; set; }
        public int? Count { get; set; }
    }

    public sealed class DashboardOrchestrationHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public int TickChannelQueued { get; set; }
        public DateTime? TickProcessorHeartbeatAt { get; set; }
        public DateTime? ThrottlingHeartbeatAt { get; set; }
        public double? TickProcessorAgeSeconds { get; set; }
        public double? ThrottlingAgeSeconds { get; set; }
    }

    public sealed class DashboardStrategyHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public int ActiveSessions { get; set; }
        public int PausedSessions { get; set; }
        public int TotalSessions { get; set; }
        public int Decisions { get; set; }
        public DateTime? LastDecisionAt { get; set; }
    }

    public sealed class DashboardIntegrations
    {
        public DashboardJevHealth Jev { get; set; } = new();
        public DashboardPythonBridgeHealth PythonBridge { get; set; } = new();
    }

    public sealed class DashboardJevHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public bool Enabled { get; set; }
        public bool HasKey { get; set; }
        public string Model { get; set; } = "";
        public DateTime? LastCallAt { get; set; }
        public string? LastResult { get; set; }
        public long? LastLatencyMs { get; set; }
        public int? LastHttpStatus { get; set; }
    }

    public sealed class DashboardPythonBridgeHealth
    {
        public string Status { get; set; } = DashboardStatus.Idle;
        public string BaseUrl { get; set; } = "";
        public bool Reachable { get; set; }
        public bool? Mt5Connected { get; set; }
        public long? LatencyMs { get; set; }
        public DateTime? CheckedAt { get; set; }
        public string? Error { get; set; }
    }

    public sealed class DashboardUnknownAgent
    {
        public int AgentCode { get; set; }
        public string AgentName { get; set; } = "";
        public List<string> RawNames { get; set; } = new();
        public List<string> Symbols { get; set; } = new();
        public long Count { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        /// <summary>tick | bubble</summary>
        public string Source { get; set; } = "";
    }

    public sealed class DashboardRecentError
    {
        public DateTime At { get; set; }
        public string Level { get; set; } = "";
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
    }

    public sealed class DashboardAgentInfo
    {
        public int Code { get; set; }
        public string Name { get; set; } = "";
        public bool Known { get; set; }
    }
}

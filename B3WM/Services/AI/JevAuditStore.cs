using B3WM.Shared.Models.AI;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Gravação best-effort da auditoria Jev em Data/JEV (1 arquivo por
    /// chamada). Nunca derruba a decisão: falha de disco vira warning + "".
    /// </summary>
    public sealed class JevAuditStore
    {
        private readonly DataKeeperBase _keeper;
        private readonly ILogger<JevAuditStore> _logger;

        public JevAuditStore(DataKeeperBase keeper, ILogger<JevAuditStore> logger)
        {
            _keeper = keeper;
            _logger = logger;
        }

        /// <summary>Grava o record; devolve o nome do arquivo ou "" em falha.</summary>
        public async Task<string> TryWriteAsync(JevAuditRecord record, CancellationToken ct = default)
        {
            try
            {
                var fileName = JevAuditFiles.BuildFileName(record.Symbol, record.At, record.Id);
                await _keeper.WriteDataAsync(JevAuditFiles.BuildPath(fileName), record);
                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[jev-audit] falha ao gravar auditoria {Symbol}", record.Symbol);
                return "";
            }
        }
    }
}

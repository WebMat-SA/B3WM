using System.Text.Json;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Orçamento do state enviado à Jev. O TypeSafe limita 32k tokens por
    /// pergunta no modo default (state + 1 pergunta); usamos 1 token/char como
    /// estimativa conservadora e miramos abaixo do limite para deixar margem.
    /// Puro, sem I/O.
    /// </summary>
    public static class JevStateBudget
    {
        /// <summary>Alvo do state + pergunta no caminho default.</summary>
        public const int TargetTokens = 26_000;

        /// <summary>Limite default por pergunta (state + 1 pergunta).</summary>
        public const int PerQuestionLimitTokens = 32_000;

        /// <summary>Limite default por requisição (state + todas as perguntas).</summary>
        public const int PerRequestLimitTokens = 64_000;

        /// <summary>Limite com o header de long context.</summary>
        public const int LongContextLimitTokens = 262_000;

        /// <summary>Estimativa conservadora: 1 token por caractere do JSON.</summary>
        public static int EstimateTokens(string? json) => json?.Length ?? 0;

        public static int EstimateTokens(object? state) =>
            state == null ? 0 : EstimateTokens(JsonSerializer.Serialize(state));

        public static bool ExceedsTarget(string? json) =>
            EstimateTokens(json) > TargetTokens;

        public static bool ExceedsPerQuestionLimit(string? json) =>
            EstimateTokens(json) > PerQuestionLimitTokens;

        public static bool NeedsLongContext(string? json) =>
            EstimateTokens(json) > TargetTokens;
    }
}

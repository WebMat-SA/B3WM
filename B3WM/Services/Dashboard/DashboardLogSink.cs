namespace B3WM.Services.Dashboard
{
    /// <summary>
    /// Captura Warning/Error/Critical de qualquer categoria no buffer do
    /// <see cref="DashboardHealthService"/>. Assim o dashboard mostra os erros
    /// reais dos serviços sem precisar instrumentar cada catch.
    /// </summary>
    public sealed class DashboardLogSink : ILoggerProvider
    {
        private readonly DashboardHealthService _health;

        public DashboardLogSink(DashboardHealthService health) => _health = health;

        public ILogger CreateLogger(string categoryName) => new SinkLogger(_health, categoryName);

        public void Dispose()
        {
        }

        private sealed class SinkLogger : ILogger
        {
            private readonly DashboardHealthService _health;
            private readonly string _category;

            public SinkLogger(DashboardHealthService health, string category)
            {
                _health = health;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var message = formatter(state, exception);
                _health.RecordError(
                    logLevel.ToString(),
                    _category,
                    message,
                    exception?.GetType().FullName,
                    exception?.Message);
            }
        }
    }
}

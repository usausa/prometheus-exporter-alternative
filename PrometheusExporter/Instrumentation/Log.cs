namespace PrometheusExporter.Instrumentation;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Instrumentation enabled. type=[{name}]")]
    public static partial void InfoInstrumentationEnabled(this ILogger logger, string name);
}

namespace PrometheusExporter.Metrics;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Metrics enabled. type=[{name}]")]
    public static partial void InfoMetricsEnabled(this ILogger logger, string name);
}

namespace PrometheusExporter.Exporter;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Export failed.")]
    public static partial void ErrorExportFailed(this ILogger logger, Exception ex);
}

namespace PrometheusExporter.Exporter;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Exporter start.")]
    public static partial void InfoExporterStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exporter stop.")]
    public static partial void InfoExporterStop(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Export failed.")]
    public static partial void ErrorExportFailed(this ILogger logger, Exception ex);
}

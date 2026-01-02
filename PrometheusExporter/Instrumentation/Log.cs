namespace PrometheusExporter.Instrumentation;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Instrumentation load failed. assemblyName=[{assemblyName}]")]
    public static partial void ErrorInstrumentationLoadFailed(this ILogger logger, Exception ex, string assemblyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Instrumentation enabled. type=[{name}]")]
    public static partial void InfoInstrumentationEnabled(this ILogger logger, string name);
}

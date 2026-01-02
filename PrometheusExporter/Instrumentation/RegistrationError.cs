namespace PrometheusExporter.Instrumentation;

internal sealed record RegistrationError(string Name, Exception Exception);

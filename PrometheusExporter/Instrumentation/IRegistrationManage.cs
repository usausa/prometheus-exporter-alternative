namespace PrometheusExporter.Instrumentation;

internal interface IRegistrationManager
{
    IReadOnlyList<Registration> Registrations { get; }

    IReadOnlyList<RegistrationError> Errors { get; }
}

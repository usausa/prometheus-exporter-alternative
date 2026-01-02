namespace PrometheusExporter.Instrumentation;

internal interface IRegistrationManager
{
    public IReadOnlyList<Registration> Registrations { get; }

    public IReadOnlyList<RegistrationError> Errors { get; }
}

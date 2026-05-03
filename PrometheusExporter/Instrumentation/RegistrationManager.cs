namespace PrometheusExporter.Instrumentation;

internal sealed class RegistrationManager : IRegistrationManager
{
    private readonly List<Registration> registrations = [];

    private readonly List<RegistrationError> errors = [];

    public IReadOnlyList<Registration> Registrations => registrations;

    public IReadOnlyList<RegistrationError> Errors => errors;

    public void Add(string name, Type type)
    {
        registrations.Add(new Registration(name, type));
    }

    public void AddError(string name, Exception exception)
    {
        errors.Add(new RegistrationError(name, exception));
    }
}

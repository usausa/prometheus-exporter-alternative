namespace PrometheusExporter.Instrumentation;

internal sealed class RegistrationManager : IRegistrationManager
{
    private readonly List<Registration> registrations = new();

    public IReadOnlyList<Registration> Registrations => registrations;

    public void Add(string name, Type type)
    {
        registrations.Add(new Registration(name, type));
    }
}

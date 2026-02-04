namespace PrometheusExporter.Instrumentation.Raspberry;

internal sealed class RaspberryOptions
{
    public int UpdateDuration { get; set; } = 1000;

    public bool Vcio { get; set; } = true;

    public bool Gpio { get; set; } = true;
}

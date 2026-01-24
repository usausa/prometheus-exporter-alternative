namespace PrometheusExporter.Metrics;

using System.Text;

internal sealed class Tag
{
    public string Key { get; }

    public string Value { get; }

    public byte[] KeyBytes { get; }

    public byte[] ValueBytes { get; }

    public Tag(string key, string value)
    {
        Key = key;
        Value = value;
        KeyBytes = Encoding.UTF8.GetBytes(key);
        ValueBytes = Encoding.UTF8.GetBytes(value);
    }
}

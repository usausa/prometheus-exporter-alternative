namespace TypeOperationBenchmark;

using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;

internal static class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<Benchmark>();
    }
}

internal class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddExporter(MarkdownExporter.GitHub);
        AddColumn(
            StatisticColumn.Mean,
            StatisticColumn.Min,
            StatisticColumn.Max,
            StatisticColumn.P90,
            StatisticColumn.Error,
            StatisticColumn.StdDev);
        AddDiagnoser(MemoryDiagnoser.Default, new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 3, printSource: true, printInstructionAddresses: true, exportDiff: true)));
    }
}

#pragma warning disable CA1305
// ReSharper disable SpecifyACultureInStringConversionExplicitly
[Config(typeof(BenchmarkConfig))]
#pragma warning disable CA1515
public class Benchmark
{
    public int IntValue { get; set; }

    [Benchmark]
    public string Convert() => IntValue.ToString();

    [Benchmark]
    public string ConvertBySwitch() => SwitchOperation2.Convert(IntValue);
}
#pragma warning restore CA1515

internal static class SwitchOperation2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Convert<T>(T value) => Converter<T>.Operator.Convert(value);

    internal interface IConverter<in T>
    {
        string Convert(T value);
    }

    internal static class Converter<T>
    {
        internal static readonly IConverter<T> Operator = ResolveOperator();

        private static IConverter<T> ResolveOperator()
        {
            if (typeof(T) == typeof(double))
            {
                return (IConverter<T>)(object)new DoubleConverter();
            }
            if (typeof(T) == typeof(int))
            {
                return (IConverter<T>)(object)new IntConverter();
            }
            if (typeof(T) == typeof(bool))
            {
                return (IConverter<T>)(object)new BoolConverter();
            }
            throw new NotSupportedException();
        }
    }

    private sealed class DoubleConverter : IConverter<double>
    {
        public string Convert(double value) => value.ToString();
    }

    private sealed class IntConverter : IConverter<int>
    {
        public string Convert(int value) => value.ToString();
    }

    private sealed class BoolConverter : IConverter<bool>
    {
        public string Convert(bool value) => value.ToString();
    }
}

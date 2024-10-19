using GreenDonut;

namespace GreenDonutV2;

internal sealed class NoopDataLoaderDiagnosticEventListener : DataLoaderDiagnosticEventListener
{
    internal static readonly NoopDataLoaderDiagnosticEventListener Default = new();
}

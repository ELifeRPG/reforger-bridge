namespace ELifeRPG.Bridge.Api.Configuration;

public sealed class DependencyHealthOptions
{
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public string BackendProbePath { get; init; } = "health";
}

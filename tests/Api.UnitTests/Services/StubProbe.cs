using ELifeRPG.Bridge.Api.Services;

namespace ELifeRPG.Bridge.Api.UnitTests.Services;

internal sealed class StubProbe(string name, ProbeOutcome outcome = default) : IDependencyProbe
{
    public string Name => name;

    public Task<ProbeOutcome> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(outcome);
}

internal sealed class ThrowingProbe(string name, Exception exception) : IDependencyProbe
{
    public string Name => name;

    public Task<ProbeOutcome> ProbeAsync(CancellationToken cancellationToken) => throw exception;
}

internal sealed class HangingProbe(string name) : IDependencyProbe
{
    public string Name => name;

    public async Task<ProbeOutcome> ProbeAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new ProbeOutcome(HealthStatus.Healthy, null);
    }
}

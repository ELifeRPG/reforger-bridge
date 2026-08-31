using System.Diagnostics;
using ELifeRPG.Bridge.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ELifeRPG.Bridge.Api.Services;

public sealed class DependencyHealthScanner(
    IEnumerable<IDependencyProbe> probes,
    DependencyHealthCache cache,
    IOptions<DependencyHealthOptions> options,
    ILogger<DependencyHealthScanner> logger) : BackgroundService
{
    private readonly IDependencyProbe[] _probes = [.. probes];
    private readonly DependencyHealthOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Dependency health scan failed unexpectedly; the scanner keeps running.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_probes.Select(probe => RunProbeAsync(probe, cancellationToken)));

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        LogTransitions(results);

        cache.Publish(new HealthReport(
            HealthStatuses.Aggregate([.. results.Select(result => result.Status)]),
            DateTimeOffset.UtcNow,
            results));
    }

    private async Task<DependencyHealth> RunProbeAsync(IDependencyProbe probe, CancellationToken stoppingToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        budget.CancelAfter(_options.ProbeTimeout);
        var started = Stopwatch.GetTimestamp();

        try
        {
            return Build(probe.Name, await probe.ProbeAsync(budget.Token), started);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            var detail = $"Did not answer within {_options.ProbeTimeout.TotalSeconds:0}s.";
            return Build(probe.Name, new ProbeOutcome(HealthStatus.Unhealthy, detail), started);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Health probe {Dependency} failed unexpectedly.", probe.Name);
            return Build(probe.Name, ProbeOutcomes.FromException(exception), started);
        }
    }

    private static DependencyHealth Build(string name, ProbeOutcome outcome, long started)
        => new(name, outcome.Status, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome.Detail);

    private void LogTransitions(IReadOnlyList<DependencyHealth> results)
    {
        var previous = cache.Current.Dependencies.ToDictionary(dependency => dependency.Name, dependency => dependency.Status);

        foreach (var result in results)
        {
            if (!previous.TryGetValue(result.Name, out var before) || before == result.Status)
            {
                continue;
            }

            if (result.Status == HealthStatus.Healthy)
            {
                logger.LogInformation("Dependency {Dependency} is {Current} (was {Previous}).", result.Name, result.Status, before);
            }
            else
            {
                logger.LogWarning("Dependency {Dependency} is {Current} (was {Previous}). {Detail}", result.Name, result.Status, before, result.Detail);
            }
        }
    }
}

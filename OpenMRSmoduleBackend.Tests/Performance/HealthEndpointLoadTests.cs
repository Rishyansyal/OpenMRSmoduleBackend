using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using OpenMRSmoduleBackend.Tests.Integration;

namespace OpenMRSmoduleBackend.Tests.Performance;

[Collection(IntegrationCollection.Name)]
public sealed class HealthEndpointLoadTests(BackendIntegrationTestFactory factory) : IAsyncLifetime
{
    private const int RequestCount = 10_000;
    private const int Concurrency = 100;

    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HealthEndpoint_HandlesConcurrentLoad_AndWritesPerformanceEvidence()
    {
        using var client = factory.CreateClient();
        var outputDir = Path.Combine(FindRepositoryRoot(), "docs", "performance");
        Directory.CreateDirectory(outputDir);

        var beforeMetrics = await client.GetStringAsync("/metrics");
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "testhost-metrics-before.prom"),
            beforeMetrics);

        var durations = new ConcurrentBag<double>();
        var statusCodes = new ConcurrentDictionary<int, int>();
        var errors = new ConcurrentBag<string>();
        var monitorRows = new ConcurrentBag<MonitorSample>();
        using var monitorCts = new CancellationTokenSource();

        var monitor = Task.Run(async () =>
        {
            var process = Process.GetCurrentProcess();
            while (!monitorCts.IsCancellationRequested)
            {
                process.Refresh();
                monitorRows.Add(new MonitorSample(
                    DateTimeOffset.UtcNow,
                    Math.Round(process.TotalProcessorTime.TotalSeconds, 3),
                    Math.Round(process.WorkingSet64 / 1024d / 1024d, 2),
                    Math.Round(process.PrivateMemorySize64 / 1024d / 1024d, 2),
                    process.Threads.Count,
                    process.HandleCount));

                await Task.Delay(TimeSpan.FromMilliseconds(250), CancellationToken.None);
            }
        });

        var startedAt = DateTimeOffset.UtcNow;
        var total = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, RequestCount),
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency },
            async (_, _) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var response = await client.GetAsync("/health");
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalMilliseconds);
                    statusCodes.AddOrUpdate((int)response.StatusCode, 1, (_, count) => count + 1);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    durations.Add(sw.Elapsed.TotalMilliseconds);
                    errors.Add(ex.GetType().Name + ": " + ex.Message);
                }
            });

        total.Stop();
        monitorCts.Cancel();
        await monitor;

        var afterMetrics = await client.GetStringAsync("/metrics");
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "testhost-metrics-after.prom"),
            afterMetrics);

        var orderedDurations = durations.Order().ToArray();
        var successCount = statusCodes
            .Where(kvp => kvp.Key >= 200 && kvp.Key < 300)
            .Sum(kvp => kvp.Value);
        var failureCount = RequestCount - successCount;

        var summary = new
        {
            startedAt,
            finishedAt = DateTimeOffset.UtcNow,
            host = "Microsoft.AspNetCore.Mvc.Testing TestServer",
            endpoint = "/health",
            requestCount = RequestCount,
            concurrency = Concurrency,
            totalDurationSeconds = Math.Round(total.Elapsed.TotalSeconds, 3),
            throughputRequestsPerSecond = Math.Round(RequestCount / total.Elapsed.TotalSeconds, 2),
            successCount,
            failureCount,
            successRatePercent = Math.Round(successCount * 100d / RequestCount, 3),
            statusCodes = statusCodes.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            latencyMs = new
            {
                min = Percentile(orderedDurations, 0),
                p50 = Percentile(orderedDurations, 50),
                p95 = Percentile(orderedDurations, 95),
                p99 = Percentile(orderedDurations, 99),
                max = Percentile(orderedDurations, 100)
            },
            monitor = new
            {
                sampleIntervalMs = 250,
                samples = monitorRows.Count,
                maxWorkingSetMb = monitorRows
                    .Select(row => row.WorkingSetMb)
                    .DefaultIfEmpty(0)
                    .Max(),
                maxPrivateMemoryMb = monitorRows
                    .Select(row => row.PrivateMemoryMb)
                    .DefaultIfEmpty(0)
                    .Max()
            },
            errors = errors.Take(10).ToArray()
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "testhost-loadtest-results.json"),
            JsonSerializer.Serialize(summary, jsonOptions));
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "testhost-loadtest-monitor.json"),
            JsonSerializer.Serialize(monitorRows.OrderBy(row => row.Timestamp), jsonOptions));

        Assert.Equal(RequestCount, successCount);
        Assert.Empty(errors);
    }

    private static double Percentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
            return 0;

        if (percentile <= 0)
            return Math.Round(orderedValues[0], 3);

        if (percentile >= 100)
            return Math.Round(orderedValues[^1], 3);

        var index = (int)Math.Ceiling(percentile / 100d * orderedValues.Length) - 1;
        index = Math.Clamp(index, 0, orderedValues.Length - 1);
        return Math.Round(orderedValues[index], 3);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenMRSmoduleBackend.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find OpenMRSmoduleBackend.csproj.");
    }

    private sealed record MonitorSample(
        DateTimeOffset Timestamp,
        double CpuSeconds,
        double WorkingSetMb,
        double PrivateMemoryMb,
        int ThreadCount,
        int HandleCount);
}

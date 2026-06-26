param(
    [string]$BaseUrl = "http://127.0.0.1:5111",
    [int]$Requests = 1000,
    [int]$Concurrency = 50,
    [string]$OutDir = "docs/performance"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$startedAt = Get-Date
$processName = "OpenMRSmoduleBackend"
$monitorPath = Join-Path $OutDir "loadtest-monitor.csv"
$resultsPath = Join-Path $OutDir "loadtest-results.json"
$metricsBeforePath = Join-Path $OutDir "metrics-before.prom"
$metricsAfterPath = Join-Path $OutDir "metrics-after.prom"

function Save-TextResponse($Url, $Path) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10
        Set-Content -Path $Path -Value $response.Content -Encoding UTF8
    }
    catch {
        Set-Content -Path $Path -Value "# scrape failed: $($_.Exception.Message)" -Encoding UTF8
    }
}

Save-TextResponse "$BaseUrl/metrics" $metricsBeforePath

"timestamp,cpu_seconds,working_set_mb,private_memory_mb,thread_count,handle_count" |
    Set-Content -Path $monitorPath -Encoding UTF8

$monitor = Start-Job -ArgumentList $processName, $monitorPath -ScriptBlock {
    param($ProcessName, $MonitorPath)
    while ($true) {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($proc) {
            $line = "{0:o},{1},{2},{3},{4},{5}" -f `
                (Get-Date),
                $proc.CPU,
                [math]::Round($proc.WorkingSet64 / 1MB, 2),
                [math]::Round($proc.PrivateMemorySize64 / 1MB, 2),
                $proc.Threads.Count,
                $proc.HandleCount
            Add-Content -Path $MonitorPath -Value $line -Encoding UTF8
        }
        Start-Sleep -Seconds 1
    }
}

$run = Measure-Command {
    $jobs = for ($worker = 0; $worker -lt $Concurrency; $worker++) {
        $count = [math]::Floor($Requests / $Concurrency)
        if ($worker -lt ($Requests % $Concurrency)) {
            $count++
        }

        Start-Job -ArgumentList $BaseUrl, $count -ScriptBlock {
            param($BaseUrl, $Count)
            $localResults = New-Object System.Collections.Generic.List[object]
            $client = [System.Net.Http.HttpClient]::new()
            $client.Timeout = [TimeSpan]::FromSeconds(15)

            for ($i = 0; $i -lt $Count; $i++) {
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $status = 0
                $errorMessage = $null
                try {
                    $response = $client.GetAsync("$BaseUrl/health").GetAwaiter().GetResult()
                    $status = [int]$response.StatusCode
                    $response.Dispose()
                }
                catch {
                    $errorMessage = $_.Exception.Message
                }
                finally {
                    $sw.Stop()
                }

                $localResults.Add([pscustomobject]@{
                    status = $status
                    durationMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 3)
                    error = $errorMessage
                }) | Out-Null
            }

            $client.Dispose()
            $localResults
        }
    }

    $results = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job
    $script:allResults = $results
}

Stop-Job $monitor | Out-Null
Remove-Job $monitor | Out-Null

Save-TextResponse "$BaseUrl/metrics" $metricsAfterPath

$durations = @($allResults | ForEach-Object { [double]$_.durationMs } | Sort-Object)
$success = @($allResults | Where-Object { $_.status -ge 200 -and $_.status -lt 300 }).Count
$failures = @($allResults | Where-Object { $_.status -lt 200 -or $_.status -ge 300 }).Count

function Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return $null }
    $index = [math]::Ceiling(($Percentile / 100) * $Values.Count) - 1
    $index = [math]::Max(0, [math]::Min($Values.Count - 1, $index))
    return [math]::Round($Values[$index], 3)
}

$summary = [pscustomobject]@{
    startedAt = $startedAt.ToString("o")
    finishedAt = (Get-Date).ToString("o")
    baseUrl = $BaseUrl
    endpoint = "/health"
    requests = $Requests
    concurrency = $Concurrency
    totalDurationSeconds = [math]::Round($run.TotalSeconds, 3)
    throughputRequestsPerSecond = [math]::Round($Requests / $run.TotalSeconds, 2)
    successCount = $success
    failureCount = $failures
    successRatePercent = [math]::Round(($success / $Requests) * 100, 3)
    minMs = if ($durations.Count) { [math]::Round($durations[0], 3) } else { $null }
    p50Ms = Percentile $durations 50
    p95Ms = Percentile $durations 95
    p99Ms = Percentile $durations 99
    maxMs = if ($durations.Count) { [math]::Round($durations[-1], 3) } else { $null }
    statusCodes = $allResults | Group-Object status | ForEach-Object {
        [pscustomobject]@{ status = $_.Name; count = $_.Count }
    }
}

$summary | ConvertTo-Json -Depth 5 | Set-Content -Path $resultsPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 5

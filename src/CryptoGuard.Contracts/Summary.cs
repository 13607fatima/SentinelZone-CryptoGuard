namespace CryptoGuard.Contracts;

public record SummaryMetric(double? Minimum,double? Maximum,double? Mean,string Unit,int ValidSamples,int TotalSamples,double CoveredSeconds);
public record ProcessSummary(string ProcessInstanceId,SummaryMetric CpuPercentHostCapacity,SummaryMetric RssBytes);
public record TelemetrySummary(DateTimeOffset StartedAt,DateTimeOffset EndedAt,double DurationMs,int SampleCount,
    SummaryMetric HostCpuPercent,SummaryMetric HostMemoryUsedPercent,SummaryMetric ReceiveBytesPerSecond,
    SummaryMetric SendBytesPerSecond,ProcessSummary[] Processes);

namespace MySCADA.Models;
public sealed class PendingRunningSample
{
    public string RecordId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string? CycleId { get; init; }
    public PlcRuntimeSnapshot Snapshot { get; init; } = new();
    public int RetryCount { get; init; }
}

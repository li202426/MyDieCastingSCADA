namespace MySCADA.Models;
public sealed class SyncOutboxItem
{
    public string Id { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityKey { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string Status { get; init; } = "Pending";
    public int RetryCount { get; init; }
    public string? LastError { get; init; }
}
public sealed class DatabaseSyncCounts
{
    public int Pending { get; init; }
    public int Failed { get; init; }
    public int Dead { get; init; }
    public int TotalWaiting => Pending + Failed;
}
public static class SyncEntityTypes
{
    public const string Alarm = "Alarm";
    public const string ProcessSnapshot = "ProcessSnapshot";
    public const string ProcessHistory = "ProcessHistory";
    public const string CycleStart = "CycleStart";
    public const string CycleComplete = "CycleComplete";
    public const string MoldSave = "MoldSave";
    public const string MoldDelete = "MoldDelete";
}
public sealed class AlarmSyncPayload { public AlarmRecord Alarm { get; set; } = new(); }
public sealed class ProcessSnapshotSyncPayload { public string DeviceName { get; set; } = ""; public string Source { get; set; } = ""; public string ProcessJson { get; set; } = ""; }
public sealed class ProcessHistorySyncPayload { public string DeviceName { get; set; } = ""; public string Source { get; set; } = ""; public PlcRuntimeSnapshot Snapshot { get; set; } = new(); }
public sealed class CycleStartSyncPayload { public string CycleId { get; set; } = ""; public string DeviceName { get; set; } = ""; public DateTime StartTime { get; set; } public string Reason { get; set; } = ""; }
public sealed class CycleCompleteSyncPayload { public string CycleId { get; set; } = ""; public DateTime EndTime { get; set; } }
public sealed class MoldSaveSyncPayload { public MoldRecord Mold { get; set; } = new(); }
public sealed class MoldDeleteSyncPayload { public string MoldNumber { get; set; } = ""; }

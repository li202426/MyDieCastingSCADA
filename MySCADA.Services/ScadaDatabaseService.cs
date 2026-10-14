using Microsoft.Data.Sqlite;
using MySCADA.Models;
using System.Text.Json;
namespace MySCADA.Services;
public sealed class ScadaDatabaseService
{
    private readonly object _sync = new();
    private readonly string _connectionString;
    private SqliteConnection? _transactionConnection;
    private SqliteTransaction? _transaction;
    public string DatabasePath { get; }
    public ScadaDatabaseService(string? path = null)
    {
        DatabasePath = path ?? Path.Combine(AppContext.BaseDirectory, "Data", "Scada.db");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = $"Data Source={DatabasePath}";
        Execute(@"
CREATE TABLE IF NOT EXISTS RunningSamples(Id INTEGER PRIMARY KEY AUTOINCREMENT,SampleTime TEXT,DeviceName TEXT,ActualPressure REAL,AluminiumTemperature REAL,FurnaceTemperature REAL,TemperaturesJson TEXT);
CREATE INDEX IF NOT EXISTS IX_RunningSamples_DeviceTime ON RunningSamples(DeviceName,SampleTime DESC);
CREATE TABLE IF NOT EXISTS ProductionCycles(CycleId TEXT PRIMARY KEY,DeviceName TEXT NOT NULL,StartTime TEXT NOT NULL,EndTime TEXT,StartReason TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_ProductionCycles_DeviceTime ON ProductionCycles(DeviceName,StartTime DESC);
CREATE TABLE IF NOT EXISTS AlarmEvents(Id INTEGER PRIMARY KEY AUTOINCREMENT,OccurredAt TEXT,RecoveredAt TEXT,DeviceName TEXT,Code TEXT,Level TEXT,Message TEXT,CurrentValue TEXT,IsActive INTEGER,IsAcknowledged INTEGER);
CREATE INDEX IF NOT EXISTS IX_AlarmEvents_DeviceActive ON AlarmEvents(DeviceName,IsActive,OccurredAt DESC);
CREATE TABLE IF NOT EXISTS ProcessSnapshots(Id INTEGER PRIMARY KEY AUTOINCREMENT,SavedAt TEXT,DeviceName TEXT,Source TEXT,ProcessJson TEXT);
CREATE TABLE IF NOT EXISTS ProcessHistory(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,SnapshotGroupId TEXT NOT NULL,SavedAt TEXT NOT NULL,DeviceName TEXT NOT NULL,Source TEXT NOT NULL,ChannelNumber INTEGER NOT NULL,
 Pressure1 REAL,Pressure2 REAL,Pressure3 REAL,Pressure4 REAL,Pressure5 REAL,Pressure6 REAL,Pressure7 REAL,Pressure8 REAL,MaintainPressure REAL,
 Time1 INTEGER,Time2 INTEGER,Time3 INTEGER,Time4 INTEGER,Time5 INTEGER,Time6 INTEGER,Time7 INTEGER,Time8 INTEGER,MaintainTime INTEGER,
 CoolingTimeMax INTEGER,CoolingTimeMin INTEGER,CastingOpenTemperature REAL,CastingOpenMode INTEGER,CastingOpenThermocoupleNm INTEGER,
 AirAuMode INTEGER,AirThermocouple INTEGER,AirTemperature REAL,AirFlowSet REAL,AirControlMode INTEGER,AirDelayTime INTEGER,AirOpenTime INTEGER,AirCrossTime INTEGER,
 WaterAuMode INTEGER,WaterThermocouple INTEGER,WaterTemperature REAL,WaterFlowSet REAL,WaterControlMode INTEGER,WaterDelayTime INTEGER,WaterOpenTime INTEGER,WaterCrossTime INTEGER);
CREATE INDEX IF NOT EXISTS IX_ProcessHistory_DeviceTime ON ProcessHistory(DeviceName,SavedAt DESC);
CREATE TABLE IF NOT EXISTS ProcessHistorySummary(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,SavedAt TEXT NOT NULL,DeviceName TEXT NOT NULL,Source TEXT NOT NULL,
 Pressure1 REAL,Pressure2 REAL,Pressure3 REAL,Pressure4 REAL,Pressure5 REAL,Pressure6 REAL,Pressure7 REAL,Pressure8 REAL,MaintainPressure REAL,
 Time1 INTEGER,Time2 INTEGER,Time3 INTEGER,Time4 INTEGER,Time5 INTEGER,Time6 INTEGER,Time7 INTEGER,Time8 INTEGER,MaintainTime INTEGER,
 CoolingTimeMax INTEGER,CoolingTimeMin INTEGER,CastingOpenTemperature REAL,CastingOpenMode INTEGER,CastingOpenThermocoupleNm INTEGER,
 AirAuModesJson TEXT,WaterAuModesJson TEXT,AirThermocouplesJson TEXT,WaterThermocouplesJson TEXT,
 AirTemperaturesJson TEXT,WaterTemperaturesJson TEXT,AirFlowSetsJson TEXT,WaterFlowSetsJson TEXT,
 AirControlModesJson TEXT,WaterControlModesJson TEXT,AirDelayTimesJson TEXT,WaterDelayTimesJson TEXT,
 AirOpenTimesJson TEXT,WaterOpenTimesJson TEXT,AirCrossTimesJson TEXT,WaterCrossTimesJson TEXT);
CREATE INDEX IF NOT EXISTS IX_ProcessHistorySummary_DeviceTime ON ProcessHistorySummary(DeviceName,SavedAt DESC);
CREATE TABLE IF NOT EXISTS Molds(Id INTEGER PRIMARY KEY AUTOINCREMENT,MoldNumber TEXT NOT NULL UNIQUE,ProductName TEXT,DrawingNumber TEXT,Remark TEXT,UpdatedAt TEXT);
CREATE TABLE IF NOT EXISTS SyncOutbox(Id TEXT PRIMARY KEY,EntityType TEXT NOT NULL,EntityKey TEXT NOT NULL,DeviceName TEXT NOT NULL,PayloadJson TEXT NOT NULL,CreatedAt TEXT NOT NULL,Status TEXT NOT NULL,RetryCount INTEGER NOT NULL DEFAULT 0,LastError TEXT,SyncedAt TEXT);
CREATE INDEX IF NOT EXISTS IX_SyncOutbox_StatusCreated ON SyncOutbox(Status,CreatedAt);
CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY AUTOINCREMENT,Username TEXT NOT NULL UNIQUE COLLATE NOCASE,PasswordHash TEXT NOT NULL,PasswordSalt TEXT NOT NULL,Role TEXT NOT NULL,IsEnabled INTEGER NOT NULL,CreatedAt TEXT NOT NULL,LastLoginAt TEXT,PasswordChangedAt TEXT NOT NULL);");
        EnsureRunningSampleColumn("ResidualAluminium", "REAL NOT NULL DEFAULT 0");
        EnsureRunningSampleColumn("SetAluminiumTemperature", "REAL NOT NULL DEFAULT 0");
        EnsureRunningSampleColumn("SetFurnaceTemperature", "REAL NOT NULL DEFAULT 0");
        EnsureRunningSampleColumn("CoolingActualFlowsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureRunningSampleColumn("CoolingSetFlowsJson", "TEXT NOT NULL DEFAULT '[]'");
        EnsureRunningSampleColumn("FullSnapshotJson", "TEXT NOT NULL DEFAULT '{}'");
        EnsureRunningSampleColumn("CycleId", "TEXT NULL");
        EnsureRunningSampleColumn("RecordId", "TEXT NULL");
        EnsureRunningSampleColumn("SyncStatus", "TEXT NOT NULL DEFAULT 'Legacy'");
        EnsureRunningSampleColumn("SyncedAt", "TEXT NULL");
        EnsureRunningSampleColumn("RetryCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureRunningSampleColumn("LastSyncError", "TEXT NULL");
        Execute("CREATE INDEX IF NOT EXISTS IX_RunningSamples_CycleId ON RunningSamples(CycleId)");
        Execute("CREATE UNIQUE INDEX IF NOT EXISTS IX_RunningSamples_RecordId ON RunningSamples(RecordId) WHERE RecordId IS NOT NULL");
        Execute("CREATE INDEX IF NOT EXISTS IX_RunningSamples_SyncStatus ON RunningSamples(SyncStatus,Id)");
    }
    public string? SaveRunningSample(string device, PlcRuntimeSnapshot s, string? cycleId = null)
    {
        if (!s.IsSuccess) return null;
        string recordId = Guid.NewGuid().ToString("D");
        int?[] actualFlows = s.CoolingChannels
            .SelectMany(x => new int?[] { x.AirActual, x.WaterActual }).ToArray();
        int[] setFlows = s.CoolingChannels
            .SelectMany(x => new[] { x.AirFlowSet, x.WaterFlowSet }).ToArray();
        Execute(@"INSERT INTO RunningSamples
(RecordId,SyncStatus,SampleTime,DeviceName,CycleId,ActualPressure,ResidualAluminium,AluminiumTemperature,SetAluminiumTemperature,FurnaceTemperature,SetFurnaceTemperature,TemperaturesJson,CoolingActualFlowsJson,CoolingSetFlowsJson,FullSnapshotJson)
VALUES($record,'Pending',$t,$d,$cycle,$p,$residual,$a,$setA,$f,$setF,$temperatures,$actualFlows,$setFlows,$snapshot);",
            ("$record", recordId),
            ("$t", s.SampleTime.ToString("O")), ("$d", device),
            ("$cycle", (object?)cycleId ?? DBNull.Value), ("$p", s.ActualPressure),
            ("$residual",s.AmountOfResidualAluminum),
            ("$a", s.ActualAluminiumTemperature), ("$setA",s.SetAluminiumTemperature),
            ("$f", s.ActualFurnaceTemperature), ("$setF",s.SetFurnaceTemperature),
            ("$temperatures", JsonSerializer.Serialize(s.Temperatures)),
            ("$actualFlows",JsonSerializer.Serialize(actualFlows)),
            ("$setFlows",JsonSerializer.Serialize(setFlows)),
            ("$snapshot",JsonSerializer.Serialize(s)));
        return recordId;
    }
    public List<PendingRunningSample> GetPendingRunningSamples(string deviceName, int count = 100)
    {
        count = Math.Clamp(count, 1, 1000);
        var rows = new List<PendingRunningSample>();
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT RecordId,DeviceName,CycleId,FullSnapshotJson,RetryCount
FROM RunningSamples
WHERE DeviceName=$device AND SyncStatus IN ('Pending','Failed')
  AND RetryCount < 10 AND RecordId IS NOT NULL
ORDER BY Id
LIMIT $count";
            command.Parameters.AddWithValue("$device", deviceName);
            command.Parameters.AddWithValue("$count", count);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string recordId = reader.GetString(0);
                try
                {
                    PlcRuntimeSnapshot? snapshot = JsonSerializer.Deserialize<PlcRuntimeSnapshot>(reader.GetString(3));
                    if (snapshot == null) throw new InvalidDataException("FullSnapshotJson为空。");
                    rows.Add(new PendingRunningSample
                    {
                        RecordId = recordId,
                        DeviceName = reader.GetString(1),
                        CycleId = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Snapshot = snapshot,
                        RetryCount = reader.GetInt32(4)
                    });
                }
                catch
                {
                    rows.Add(new PendingRunningSample
                    {
                        RecordId = recordId,
                        DeviceName = reader.GetString(1),
                        CycleId = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Snapshot = new PlcRuntimeSnapshot { IsSuccess = false },
                        RetryCount = reader.GetInt32(4)
                    });
                }
            }
        }
        return rows;
    }
    public List<PendingRunningSample> GetPendingRunningSamples(int count = 100)
    {
        count = Math.Clamp(count, 1, 1000);
        var rows = new List<PendingRunningSample>();
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT RecordId,DeviceName,CycleId,FullSnapshotJson,RetryCount
FROM RunningSamples WHERE SyncStatus IN ('Pending','Failed') AND RetryCount < 10 AND RecordId IS NOT NULL
ORDER BY Id LIMIT $count";
            command.Parameters.AddWithValue("$count", count);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string recordId = reader.GetString(0);
                PlcRuntimeSnapshot snapshot;
                try { snapshot = JsonSerializer.Deserialize<PlcRuntimeSnapshot>(reader.GetString(3)) ?? new PlcRuntimeSnapshot { IsSuccess=false }; }
                catch { snapshot = new PlcRuntimeSnapshot { IsSuccess=false }; }
                rows.Add(new PendingRunningSample { RecordId=recordId,DeviceName=reader.GetString(1),CycleId=reader.IsDBNull(2)?null:reader.GetString(2),Snapshot=snapshot,RetryCount=reader.GetInt32(4) });
            }
        }
        return rows;
    }
    public void MarkRunningSampleSynced(string recordId) =>
        Execute(@"UPDATE RunningSamples
SET SyncStatus='Synced',SyncedAt=$time,LastSyncError=NULL
WHERE RecordId=$id",
            ("$time",DateTime.Now.ToString("O")),("$id",recordId));
    public void MarkRunningSampleSyncFailed(string recordId, string error) =>
        Execute(@"UPDATE RunningSamples
SET SyncStatus=CASE WHEN RetryCount>=9 THEN 'Dead' ELSE 'Failed' END,
    RetryCount=RetryCount+1,LastSyncError=$error
WHERE RecordId=$id",
            ("$error",error.Length > 1000 ? error[..1000] : error),("$id",recordId));
    public int GetPendingRunningSampleCount(string deviceName)
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT COUNT(*) FROM RunningSamples
WHERE DeviceName=$device AND SyncStatus IN ('Pending','Failed')";
            command.Parameters.AddWithValue("$device", deviceName);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
    public int GetPendingRunningSampleCount() => ScalarInt("SELECT COUNT(*) FROM RunningSamples WHERE SyncStatus IN ('Pending','Failed')");
    public int GetDeadRunningSampleCount() => ScalarInt("SELECT COUNT(*) FROM RunningSamples WHERE SyncStatus='Dead'");
    public string QueueSyncOperation(string entityType,string entityKey,string deviceName,object payload)
    {
        string id=Guid.NewGuid().ToString("D");
        Execute(@"INSERT INTO SyncOutbox(Id,EntityType,EntityKey,DeviceName,PayloadJson,CreatedAt,Status,RetryCount)
VALUES($id,$type,$key,$device,$payload,$created,'Pending',0)",
            ("$id",id),("$type",entityType),("$key",entityKey),("$device",deviceName),("$payload",JsonSerializer.Serialize(payload)),("$created",DateTime.Now.ToString("O")));
        return id;
    }
    public void SaveProcessSnapshotWithSync(string device,string source,DieCastingProcess process) =>
        RunInTransaction(() =>
        {
            string json=JsonSerializer.Serialize(process);
            SaveProcessSnapshotJson(device,source,json);
            QueueSyncOperation(SyncEntityTypes.ProcessSnapshot,Guid.NewGuid().ToString("D"),device,
                new ProcessSnapshotSyncPayload{DeviceName=device,Source=source,ProcessJson=json});
        });
    public void SaveProcessHistorySummaryWithSync(string device,string source,PlcRuntimeSnapshot snapshot) =>
        RunInTransaction(() =>
        {
            SaveProcessHistorySummary(device,source,snapshot);
            QueueSyncOperation(SyncEntityTypes.ProcessHistory,Guid.NewGuid().ToString("D"),device,
                new ProcessHistorySyncPayload{DeviceName=device,Source=source,Snapshot=snapshot});
        });
    public void StartProductionCycleWithSync(string cycleId,string device,DateTime startTime,string reason) =>
        RunInTransaction(() =>
        {
            StartProductionCycle(cycleId,device,startTime,reason);
            QueueSyncOperation(SyncEntityTypes.CycleStart,cycleId,device,
                new CycleStartSyncPayload{CycleId=cycleId,DeviceName=device,StartTime=startTime,Reason=reason});
        });
    public void CompleteProductionCycleWithSync(string cycleId,string device,DateTime endTime) =>
        RunInTransaction(() =>
        {
            CompleteProductionCycle(cycleId,endTime);
            QueueSyncOperation(SyncEntityTypes.CycleComplete,cycleId,device,
                new CycleCompleteSyncPayload{CycleId=cycleId,EndTime=endTime});
        });
    public long InsertAlarmWithSync(AlarmRecord alarm)
    {
        long id=0;
        RunInTransaction(() =>
        {
            id=InsertAlarm(alarm);
            QueueSyncOperation(SyncEntityTypes.Alarm,$"{alarm.DeviceName}:{alarm.Code}:{alarm.OccurredAt:O}",alarm.DeviceName,
                new AlarmSyncPayload{Alarm=alarm});
        });
        return id;
    }
    public void UpdateAlarmWithSync(AlarmRecord alarm) => RunInTransaction(() =>
    {
        UpdateAlarm(alarm);
        QueueSyncOperation(SyncEntityTypes.Alarm,$"{alarm.DeviceName}:{alarm.Code}:{alarm.OccurredAt:O}",alarm.DeviceName,
            new AlarmSyncPayload{Alarm=alarm});
    });
    public void SaveMoldWithSync(MoldRecord mold) => RunInTransaction(() =>
    {
        SaveMold(mold);
        QueueSyncOperation(SyncEntityTypes.MoldSave,mold.MoldNumber,"",new MoldSaveSyncPayload{Mold=mold});
    });
    public void DeleteMoldWithSync(long id,string moldNumber) => RunInTransaction(() =>
    {
        DeleteMold(id);
        QueueSyncOperation(SyncEntityTypes.MoldDelete,moldNumber,"",new MoldDeleteSyncPayload{MoldNumber=moldNumber});
    });
    public List<SyncOutboxItem> GetPendingSyncOperations(int count=100) => GetSyncOperations("Status IN ('Pending','Failed') AND RetryCount < 10",count);
    public List<SyncOutboxItem> GetFailedSyncOperations(int count=500) => GetSyncOperations("Status IN ('Failed','Dead')",count);
    public List<SyncOutboxItem> GetFailedRunningSampleOperations(int count=500)
    {
        var rows=new List<SyncOutboxItem>();count=Math.Clamp(count,1,1000);
        lock(_sync)
        {
            using var c=Open();using var cmd=c.CreateCommand();
            cmd.CommandText=@"SELECT RecordId,DeviceName,SampleTime,SyncStatus,RetryCount,LastSyncError
FROM RunningSamples WHERE SyncStatus IN ('Failed','Dead') ORDER BY Id DESC LIMIT $count";
            cmd.Parameters.AddWithValue("$count",count);using var r=cmd.ExecuteReader();
            while(r.Read())rows.Add(new SyncOutboxItem{Id=r.GetString(0),EntityType="RunningSample",EntityKey=r.GetString(0),DeviceName=r.GetString(1),CreatedAt=DateTime.Parse(r.GetString(2)),Status=r.GetString(3),RetryCount=r.GetInt32(4),LastError=r.IsDBNull(5)?null:r.GetString(5)});
        }
        return rows;
    }
    public DatabaseSyncCounts GetSyncCounts() => new()
    {
        Pending=ScalarInt("SELECT COUNT(*) FROM SyncOutbox WHERE Status='Pending'")+ScalarInt("SELECT COUNT(*) FROM RunningSamples WHERE SyncStatus='Pending'"),
        Failed=ScalarInt("SELECT COUNT(*) FROM SyncOutbox WHERE Status='Failed'")+ScalarInt("SELECT COUNT(*) FROM RunningSamples WHERE SyncStatus='Failed'"),
        Dead=ScalarInt("SELECT COUNT(*) FROM SyncOutbox WHERE Status='Dead'")+GetDeadRunningSampleCount()
    };
    public void MarkSyncOperationSynced(string id) => Execute("UPDATE SyncOutbox SET Status='Synced',SyncedAt=$time,LastError=NULL WHERE Id=$id",("$time",DateTime.Now.ToString("O")),("$id",id));
    public void MarkSyncOperationFailed(string id,string error) => Execute(@"UPDATE SyncOutbox SET Status=CASE WHEN RetryCount>=9 THEN 'Dead' ELSE 'Failed' END,RetryCount=RetryCount+1,LastError=$error WHERE Id=$id",("$error",TrimError(error)),("$id",id));
    public void RetryFailedSyncOperations()
    {
        Execute("UPDATE SyncOutbox SET Status='Pending',RetryCount=0,LastError=NULL WHERE Status IN ('Failed','Dead')");
        Execute("UPDATE RunningSamples SET SyncStatus='Pending',RetryCount=0,LastSyncError=NULL WHERE SyncStatus IN ('Failed','Dead')");
    }
    private List<SyncOutboxItem> GetSyncOperations(string where,int count)
    {
        var rows=new List<SyncOutboxItem>(); count=Math.Clamp(count,1,1000);
        lock(_sync)
        {
            using var c=Open(); using var cmd=c.CreateCommand();
            cmd.CommandText=$"SELECT Id,EntityType,EntityKey,DeviceName,PayloadJson,CreatedAt,Status,RetryCount,LastError FROM SyncOutbox WHERE {where} ORDER BY CreatedAt LIMIT $count";
            cmd.Parameters.AddWithValue("$count",count); using var r=cmd.ExecuteReader();
            while(r.Read())rows.Add(new SyncOutboxItem{Id=r.GetString(0),EntityType=r.GetString(1),EntityKey=r.GetString(2),DeviceName=r.GetString(3),PayloadJson=r.GetString(4),CreatedAt=DateTime.Parse(r.GetString(5)),Status=r.GetString(6),RetryCount=r.GetInt32(7),LastError=r.IsDBNull(8)?null:r.GetString(8)});
        }
        return rows;
    }
    private int ScalarInt(string sql)
    { lock(_sync){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToInt32(cmd.ExecuteScalar());} }
    private static string TrimError(string error)=>error.Length>1000?error[..1000]:error;
    public void StartProductionCycle(
        string cycleId, string device, DateTime startTime, string startReason) =>
        Execute(@"INSERT OR IGNORE INTO ProductionCycles(CycleId,DeviceName,StartTime,StartReason)
VALUES($id,$device,$start,$reason)",
            ("$id",cycleId),("$device",device),("$start",startTime.ToString("O")),("$reason",startReason));
    public void CompleteProductionCycle(string cycleId, DateTime endTime) =>
        Execute("UPDATE ProductionCycles SET EndTime=$end WHERE CycleId=$id AND EndTime IS NULL",
            ("$end",endTime.ToString("O")),("$id",cycleId));
        public (string CycleId, DateTime StartTime)? GetOpenProductionCycle(string device)
    {
        lock (_sync)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT CycleId,StartTime
FROM ProductionCycles
WHERE DeviceName=$device AND EndTime IS NULL
ORDER BY StartTime DESC
LIMIT 1";
            command.Parameters.AddWithValue("$device", device);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            return (reader.GetString(0), DateTime.Parse(reader.GetString(1)));
        }
    }
    public void DeleteRunningSamplesOlderThan(DateTime cutoff) =>
        Execute("DELETE FROM RunningSamples WHERE SampleTime < $cutoff",
            ("$cutoff", cutoff.ToString("O")));
    public List<StoredRunningSample> GetRecentRunningSamples(int count = 200)
        => GetRunningSamples(null, null, count);
    public List<StoredRunningSample> GetRunningSamples(DateTime? startTime, DateTime? endTime, int count = 200)
    {
        var rows = new List<StoredRunningSample>();
        count = Math.Clamp(count, 1, 5000);
        lock (_sync)
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT Id,SampleTime,DeviceName,CycleId,ActualPressure,ResidualAluminium,AluminiumTemperature,SetAluminiumTemperature,FurnaceTemperature,SetFurnaceTemperature,TemperaturesJson,CoolingActualFlowsJson,CoolingSetFlowsJson,FullSnapshotJson
FROM RunningSamples
WHERE ($start IS NULL OR SampleTime >= $start)
  AND ($end IS NULL OR SampleTime < $end)
ORDER BY SampleTime DESC
LIMIT $n";
            cmd.Parameters.AddWithValue("$start", startTime.HasValue ? startTime.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("$end", endTime.HasValue ? endTime.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("$n", count);
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add(new StoredRunningSample
            {
                Id=r.GetInt64(0), SampleTime=DateTime.Parse(r.GetString(1)), DeviceName=r.GetString(2),
                CycleId=r.IsDBNull(3)?null:r.GetString(3),
                ActualPressure=r.GetFloat(4), ResidualAluminium=r.GetFloat(5),
                AluminiumTemperature=r.GetFloat(6), SetAluminiumTemperature=r.GetFloat(7),
                FurnaceTemperature=r.GetFloat(8), SetFurnaceTemperature=r.GetFloat(9),
                TemperaturesJson=r.GetString(10), CoolingActualFlowsJson=r.GetString(11), CoolingSetFlowsJson=r.GetString(12),
                FullSnapshotJson=r.GetString(13)
            });
        }
        return rows;
    }
    public void SaveProcessSnapshot(string device, string source, DieCastingProcess process) =>
        SaveProcessSnapshotJson(device,source,JsonSerializer.Serialize(process));
    public void SaveProcessSnapshotJson(string device, string source, string processJson) =>
        Execute("INSERT INTO ProcessSnapshots(SavedAt,DeviceName,Source,ProcessJson) VALUES($t,$d,$s,$j)",
            ("$t",DateTime.Now.ToString("O")),("$d",device),("$s",source),("$j",processJson));
    public void SaveProcessHistory(string device,string source,PlcRuntimeSnapshot snapshot)
    {
        string groupId=Guid.NewGuid().ToString("D");
        string savedAt=DateTime.Now.ToString("O");
        lock(_sync)
        {
            using var connection=Open();
            using var transaction=connection.BeginTransaction();
            foreach(CoolingChannelItem channel in snapshot.ProcessCoolingChannels)
            {
                using var cmd=connection.CreateCommand();
                cmd.Transaction=transaction;
                cmd.CommandText=@"INSERT INTO ProcessHistory(
SnapshotGroupId,SavedAt,DeviceName,Source,ChannelNumber,
Pressure1,Pressure2,Pressure3,Pressure4,Pressure5,Pressure6,Pressure7,Pressure8,MaintainPressure,
Time1,Time2,Time3,Time4,Time5,Time6,Time7,Time8,MaintainTime,
CoolingTimeMax,CoolingTimeMin,CastingOpenTemperature,CastingOpenMode,CastingOpenThermocoupleNm,
AirAuMode,AirThermocouple,AirTemperature,AirFlowSet,AirControlMode,AirDelayTime,AirOpenTime,AirCrossTime,
WaterAuMode,WaterThermocouple,WaterTemperature,WaterFlowSet,WaterControlMode,WaterDelayTime,WaterOpenTime,WaterCrossTime)
VALUES($group,$saved,$device,$source,$channel,
$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$pm,$t1,$t2,$t3,$t4,$t5,$t6,$t7,$t8,$tm,
$coolMax,$coolMin,$openTemp,$openMode,$openTc,
$airAu,$airTc,$airTemp,$airFlow,$airMode,$airDelay,$airOpen,$airCross,
$waterAu,$waterTc,$waterTemp,$waterFlow,$waterMode,$waterDelay,$waterOpen,$waterCross)";
                Add(cmd,
                    ("$group",groupId),("$saved",savedAt),("$device",device),("$source",source),("$channel",channel.Channel),
                    ("$p1",At(snapshot.PressureValues,0)),("$p2",At(snapshot.PressureValues,1)),("$p3",At(snapshot.PressureValues,2)),
                    ("$p4",At(snapshot.PressureValues,3)),("$p5",At(snapshot.PressureValues,4)),("$p6",At(snapshot.PressureValues,5)),
                    ("$p7",At(snapshot.PressureValues,6)),("$p8",At(snapshot.PressureValues,7)),("$pm",At(snapshot.PressureValues,8)),
                    ("$t1",At(snapshot.TimeValues,0)),("$t2",At(snapshot.TimeValues,1)),("$t3",At(snapshot.TimeValues,2)),
                    ("$t4",At(snapshot.TimeValues,3)),("$t5",At(snapshot.TimeValues,4)),("$t6",At(snapshot.TimeValues,5)),
                    ("$t7",At(snapshot.TimeValues,6)),("$t8",At(snapshot.TimeValues,7)),("$tm",At(snapshot.TimeValues,8)),
                    ("$coolMax",snapshot.CoolingTimeMax),("$coolMin",snapshot.CoolingTimeMin),("$openTemp",snapshot.CastingOpenTemperature),
                    ("$openMode",snapshot.CastingOpenMode),("$openTc",snapshot.CastingOpenThermocoupleNm),
                    ("$airAu",channel.AirAuMode),("$airTc",channel.AirTheNm),("$airTemp",channel.AirTemp),("$airFlow",channel.AirFlowSet),
                    ("$airMode",channel.AirConMode),("$airDelay",channel.AirDelayTime),("$airOpen",channel.AirOpenTime),("$airCross",channel.AirCrossTime),
                    ("$waterAu",channel.WaterAuMode),("$waterTc",channel.WaterTheNm),("$waterTemp",channel.WaterTemp),("$waterFlow",channel.WaterFlowSet),
                    ("$waterMode",channel.WaterConMode),("$waterDelay",channel.WaterDelayTime),("$waterOpen",channel.WaterOpenTime),("$waterCross",channel.WaterCrossTime));
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
    public void SaveProcessHistorySummary(string device,string source,PlcRuntimeSnapshot snapshot)
    {
        var c=snapshot.ProcessCoolingChannels;
        Execute(@"INSERT INTO ProcessHistorySummary(
SavedAt,DeviceName,Source,Pressure1,Pressure2,Pressure3,Pressure4,Pressure5,Pressure6,Pressure7,Pressure8,MaintainPressure,
Time1,Time2,Time3,Time4,Time5,Time6,Time7,Time8,MaintainTime,CoolingTimeMax,CoolingTimeMin,CastingOpenTemperature,CastingOpenMode,CastingOpenThermocoupleNm,
AirAuModesJson,WaterAuModesJson,AirThermocouplesJson,WaterThermocouplesJson,AirTemperaturesJson,WaterTemperaturesJson,AirFlowSetsJson,WaterFlowSetsJson,
AirControlModesJson,WaterControlModesJson,AirDelayTimesJson,WaterDelayTimesJson,AirOpenTimesJson,WaterOpenTimesJson,AirCrossTimesJson,WaterCrossTimesJson)
VALUES($saved,$device,$source,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$pm,$t1,$t2,$t3,$t4,$t5,$t6,$t7,$t8,$tm,$coolMax,$coolMin,$openTemp,$openMode,$openTc,
$airAu,$waterAu,$airTc,$waterTc,$airTemp,$waterTemp,$airFlow,$waterFlow,$airMode,$waterMode,$airDelay,$waterDelay,$airOpen,$waterOpen,$airCross,$waterCross)",
            ("$saved",DateTime.Now.ToString("O")),("$device",device),("$source",source),
            ("$p1",At(snapshot.PressureValues,0)),("$p2",At(snapshot.PressureValues,1)),("$p3",At(snapshot.PressureValues,2)),("$p4",At(snapshot.PressureValues,3)),
            ("$p5",At(snapshot.PressureValues,4)),("$p6",At(snapshot.PressureValues,5)),("$p7",At(snapshot.PressureValues,6)),("$p8",At(snapshot.PressureValues,7)),("$pm",At(snapshot.PressureValues,8)),
            ("$t1",At(snapshot.TimeValues,0)),("$t2",At(snapshot.TimeValues,1)),("$t3",At(snapshot.TimeValues,2)),("$t4",At(snapshot.TimeValues,3)),
            ("$t5",At(snapshot.TimeValues,4)),("$t6",At(snapshot.TimeValues,5)),("$t7",At(snapshot.TimeValues,6)),("$t8",At(snapshot.TimeValues,7)),("$tm",At(snapshot.TimeValues,8)),
            ("$coolMax",snapshot.CoolingTimeMax),("$coolMin",snapshot.CoolingTimeMin),("$openTemp",snapshot.CastingOpenTemperature),
            ("$openMode",snapshot.CastingOpenMode),("$openTc",snapshot.CastingOpenThermocoupleNm),
            ("$airAu",JsonSerializer.Serialize(c.Select(x=>x.AirAuMode))),("$waterAu",JsonSerializer.Serialize(c.Select(x=>x.WaterAuMode))),
            ("$airTc",JsonSerializer.Serialize(c.Select(x=>x.AirTheNm))),("$waterTc",JsonSerializer.Serialize(c.Select(x=>x.WaterTheNm))),
            ("$airTemp",JsonSerializer.Serialize(c.Select(x=>x.AirTemp))),("$waterTemp",JsonSerializer.Serialize(c.Select(x=>x.WaterTemp))),
            ("$airFlow",JsonSerializer.Serialize(c.Select(x=>x.AirFlowSet))),("$waterFlow",JsonSerializer.Serialize(c.Select(x=>x.WaterFlowSet))),
            ("$airMode",JsonSerializer.Serialize(c.Select(x=>x.AirConMode))),("$waterMode",JsonSerializer.Serialize(c.Select(x=>x.WaterConMode))),
            ("$airDelay",JsonSerializer.Serialize(c.Select(x=>x.AirDelayTime))),("$waterDelay",JsonSerializer.Serialize(c.Select(x=>x.WaterDelayTime))),
            ("$airOpen",JsonSerializer.Serialize(c.Select(x=>x.AirOpenTime))),("$waterOpen",JsonSerializer.Serialize(c.Select(x=>x.WaterOpenTime))),
            ("$airCross",JsonSerializer.Serialize(c.Select(x=>x.AirCrossTime))),("$waterCross",JsonSerializer.Serialize(c.Select(x=>x.WaterCrossTime))));
    }
    public long InsertAlarm(AlarmRecord a)
    {
        lock (_sync)
        {
            if(_transactionConnection!=null)
            {
                using var transactionCommand=_transactionConnection.CreateCommand();transactionCommand.Transaction=_transaction;
                transactionCommand.CommandText="INSERT INTO AlarmEvents(OccurredAt,DeviceName,Code,Level,Message,CurrentValue,IsActive,IsAcknowledged) VALUES($t,$d,$c,$l,$m,$v,1,0); SELECT last_insert_rowid();";
                Add(transactionCommand,("$t",a.OccurredAt.ToString("O")),("$d",a.DeviceName),("$c",a.Code),("$l",a.Level),("$m",a.Message),("$v",a.CurrentValue));
                return (long)(transactionCommand.ExecuteScalar()??0L);
            }
            using var c=Open(); using var cmd=c.CreateCommand();
            cmd.CommandText="INSERT INTO AlarmEvents(OccurredAt,DeviceName,Code,Level,Message,CurrentValue,IsActive,IsAcknowledged) VALUES($t,$d,$c,$l,$m,$v,1,0); SELECT last_insert_rowid();";
            Add(cmd,("$t",a.OccurredAt.ToString("O")),("$d",a.DeviceName),("$c",a.Code),("$l",a.Level),("$m",a.Message),("$v",a.CurrentValue));
            return (long)(cmd.ExecuteScalar() ?? 0L);
        }
    }
    public List<AlarmRecord> GetRecentAlarms(int count = 500)
    {
        var rows=new List<AlarmRecord>(); lock(_sync) { using var c=Open(); using var cmd=c.CreateCommand();
            cmd.CommandText="SELECT Id,OccurredAt,RecoveredAt,DeviceName,Code,Level,Message,CurrentValue,IsActive,IsAcknowledged FROM AlarmEvents ORDER BY Id DESC LIMIT $n";cmd.Parameters.AddWithValue("$n",count);using var r=cmd.ExecuteReader();
            while(r.Read()) rows.Add(new AlarmRecord{Id=r.GetInt64(0),OccurredAt=DateTime.Parse(r.GetString(1)),RecoveredAt=r.IsDBNull(2)?null:DateTime.Parse(r.GetString(2)),DeviceName=r.GetString(3),Code=r.GetString(4),Level=r.GetString(5),Message=r.GetString(6),CurrentValue=r.GetString(7),IsActive=r.GetInt32(8)==1,IsAcknowledged=r.GetInt32(9)==1}); }
        return rows;
    }
    public void UpdateAlarm(AlarmRecord a) => Execute("UPDATE AlarmEvents SET RecoveredAt=$r,CurrentValue=$v,IsActive=$active,IsAcknowledged=$ack WHERE Id=$id",
        ("$r",(object?)a.RecoveredAt?.ToString("O") ?? DBNull.Value),("$v",a.CurrentValue),("$active",a.IsActive?1:0),("$ack",a.IsAcknowledged?1:0),("$id",a.Id));
    public List<MoldRecord> GetMolds()
    {
        var rows=new List<MoldRecord>(); lock(_sync) { using var c=Open(); using var cmd=c.CreateCommand();
            cmd.CommandText="SELECT Id,MoldNumber,ProductName,DrawingNumber,Remark,UpdatedAt FROM Molds ORDER BY MoldNumber"; using var r=cmd.ExecuteReader();
            while(r.Read()) rows.Add(new MoldRecord{Id=r.GetInt64(0),MoldNumber=r.GetString(1),ProductName=r.GetString(2),DrawingNumber=r.GetString(3),Remark=r.GetString(4),UpdatedAt=DateTime.Parse(r.GetString(5))}); }
        return rows;
    }
    public void SaveMold(MoldRecord m) => Execute(@"INSERT INTO Molds(MoldNumber,ProductName,DrawingNumber,Remark,UpdatedAt) VALUES($n,$p,$d,$r,$t)
ON CONFLICT(MoldNumber) DO UPDATE SET ProductName=$p,DrawingNumber=$d,Remark=$r,UpdatedAt=$t",
        ("$n",m.MoldNumber.Trim()),("$p",m.ProductName.Trim()),("$d",m.DrawingNumber.Trim()),("$r",m.Remark.Trim()),("$t",DateTime.Now.ToString("O")));
    public void DeleteMold(long id) => Execute("DELETE FROM Molds WHERE Id=$id",("$id",id));
    public int GetUserCount()=>ScalarInt("SELECT COUNT(*) FROM Users");
    public void InsertUser(UserAccount user)=>Execute(@"INSERT INTO Users(Username,PasswordHash,PasswordSalt,Role,IsEnabled,CreatedAt,PasswordChangedAt)
VALUES($name,$hash,$salt,$role,$enabled,$created,$changed)",("$name",user.Username),("$hash",user.PasswordHash),("$salt",user.PasswordSalt),("$role",user.Role.ToString()),("$enabled",user.IsEnabled?1:0),("$created",user.CreatedAt.ToString("O")),("$changed",user.PasswordChangedAt.ToString("O")));
    public UserAccount? FindUser(string username)
    {
        lock(_sync){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,Username,PasswordHash,PasswordSalt,Role,IsEnabled,CreatedAt,LastLoginAt,PasswordChangedAt FROM Users WHERE Username=$name";cmd.Parameters.AddWithValue("$name",username);using var r=cmd.ExecuteReader();return r.Read()?ReadUser(r):null;}
    }
    public List<UserAccount> GetUsers()
    {
        var rows=new List<UserAccount>();lock(_sync){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Id,Username,PasswordHash,PasswordSalt,Role,IsEnabled,CreatedAt,LastLoginAt,PasswordChangedAt FROM Users ORDER BY Username";using var r=cmd.ExecuteReader();while(r.Read())rows.Add(ReadUser(r));}return rows;
    }
    public void UpdateUserLogin(long id,DateTime time)=>Execute("UPDATE Users SET LastLoginAt=$time WHERE Id=$id",("$time",time.ToString("O")),("$id",id));
    public void SetUserEnabled(long id,bool enabled)=>Execute("UPDATE Users SET IsEnabled=$enabled WHERE Id=$id",("$enabled",enabled?1:0),("$id",id));
    private static UserAccount ReadUser(SqliteDataReader r)=>new(){Id=r.GetInt64(0),Username=r.GetString(1),PasswordHash=r.GetString(2),PasswordSalt=r.GetString(3),Role=Enum.TryParse(r.GetString(4),out UserRole role)?role:UserRole.Operator,IsEnabled=r.GetInt32(5)==1,CreatedAt=DateTime.Parse(r.GetString(6)),LastLoginAt=r.IsDBNull(7)?null:DateTime.Parse(r.GetString(7)),PasswordChangedAt=DateTime.Parse(r.GetString(8))};
    private void Execute(string sql, params (string Name, object Value)[] values)
    {
        lock(_sync)
        {
            if(_transactionConnection!=null)
            {
                using var transactionCommand=_transactionConnection.CreateCommand();transactionCommand.Transaction=_transaction;transactionCommand.CommandText=sql;Add(transactionCommand,values);transactionCommand.ExecuteNonQuery();return;
            }
            using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;Add(cmd,values);cmd.ExecuteNonQuery();
        }
    }
    private void RunInTransaction(Action action)
    {
        lock(_sync)
        {
            if(_transactionConnection!=null){action();return;}
            using var connection=Open();using var transaction=connection.BeginTransaction();
            _transactionConnection=connection;_transaction=transaction;
            try{action();transaction.Commit();}
            catch{try{transaction.Rollback();}catch{}throw;}
            finally{_transaction=null;_transactionConnection=null;}
        }
    }
    private static void Add(SqliteCommand cmd, params (string Name, object Value)[] values)
    { foreach(var p in values) cmd.Parameters.AddWithValue(p.Name,p.Value); }
    private static float At(float[] values,int index) => index<values.Length?values[index]:0f;
    private static int At(int[] values,int index) => index<values.Length?values[index]:0;
    private SqliteConnection Open() { var c=new SqliteConnection(_connectionString); c.Open(); return c; }
    private void EnsureRunningSampleColumn(string columnName, string definition)
    {
        lock (_sync)
        {
            using var connection=Open();
            using var check=connection.CreateCommand();
            check.CommandText="SELECT COUNT(*) FROM pragma_table_info('RunningSamples') WHERE name=$name";
            check.Parameters.AddWithValue("$name",columnName);
            if(Convert.ToInt32(check.ExecuteScalar())>0) return;
            using var alter=connection.CreateCommand();
            alter.CommandText=$"ALTER TABLE RunningSamples ADD COLUMN {columnName} {definition}";
            alter.ExecuteNonQuery();
        }
    }
}

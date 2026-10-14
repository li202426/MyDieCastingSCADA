using Microsoft.Data.SqlClient;
using MySCADA.Models;
using System.IO;
using System.Text.Json;
namespace MySCADA.Services;
public sealed class SqlServerDatabaseService
{
 private readonly DatabaseSettings _settings;
 private readonly object _sync=new();
 private SqlConnection? _transactionConnection;
 private SqlTransaction? _transaction;
 public bool IsEnabled=>_settings.SqlServerEnabled;
 public bool IsWritingEnabled{get;private set;}
 public bool CanWrite=>IsEnabled&&IsWritingEnabled&&IsAvailable;
 public int RunningDataRetentionDays=>Math.Max(0,_settings.RunningDataRetentionDays);
 public bool IsAvailable{get;private set;}
 public string LastStatusMessage{get;private set;}="尚未检测 SQL Server";
 public SqlServerDatabaseService(string? configPath=null)
 {
  configPath??=Path.Combine(AppContext.BaseDirectory,"Configs","DatabaseSettings.json");
  _settings=File.Exists(configPath)
   ? JsonSerializer.Deserialize<DatabaseSettings>(File.ReadAllText(configPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new()
   : new();
 }
 public bool TryInitialize(out string message)
 {
  if(!IsEnabled)
  {
   message="SQL Server远程存储未启用，请在 Configs/DatabaseSettings.json 中设置 SqlServerEnabled=true";
   LastStatusMessage=message;
   IsAvailable=false;
   return false;
  }
  try
  {
   Execute(@"IF OBJECT_ID('dbo.ScadaRunningSamples','U') IS NULL
CREATE TABLE dbo.ScadaRunningSamples(Id BIGINT IDENTITY PRIMARY KEY,RecordId NVARCHAR(36) NULL,SampleTime DATETIME2 NOT NULL,DeviceName NVARCHAR(100) NOT NULL,CycleId UNIQUEIDENTIFIER NULL,ActualPressure REAL NOT NULL,ResidualAluminium REAL NOT NULL,AluminiumTemperature REAL NOT NULL,SetAluminiumTemperature REAL NOT NULL,FurnaceTemperature REAL NOT NULL,SetFurnaceTemperature REAL NOT NULL,TemperaturesJson NVARCHAR(MAX) NOT NULL,CoolingActualFlowsJson NVARCHAR(MAX) NOT NULL,CoolingSetFlowsJson NVARCHAR(MAX) NOT NULL,FullSnapshotJson NVARCHAR(MAX) NOT NULL);");
   Execute("IF COL_LENGTH('dbo.ScadaRunningSamples','RecordId') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD RecordId NVARCHAR(36) NULL;");
   Execute("IF COL_LENGTH('dbo.ScadaRunningSamples','CycleId') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD CycleId UNIQUEIDENTIFIER NULL;");
   Execute(@"
IF OBJECT_ID('dbo.ScadaRunningSamples','U') IS NULL CREATE TABLE dbo.ScadaRunningSamples(Id BIGINT IDENTITY PRIMARY KEY,RecordId NVARCHAR(36) NULL,SampleTime DATETIME2 NOT NULL,DeviceName NVARCHAR(100) NOT NULL,CycleId UNIQUEIDENTIFIER NULL,ActualPressure REAL NOT NULL,ResidualAluminium REAL NOT NULL,AluminiumTemperature REAL NOT NULL,SetAluminiumTemperature REAL NOT NULL,FurnaceTemperature REAL NOT NULL,SetFurnaceTemperature REAL NOT NULL,TemperaturesJson NVARCHAR(MAX) NOT NULL,CoolingActualFlowsJson NVARCHAR(MAX) NOT NULL,CoolingSetFlowsJson NVARCHAR(MAX) NOT NULL,FullSnapshotJson NVARCHAR(MAX) NOT NULL);
IF COL_LENGTH('dbo.ScadaRunningSamples','RecordId') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD RecordId NVARCHAR(36) NULL;
IF COL_LENGTH('dbo.ScadaRunningSamples','CycleId') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD CycleId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.ScadaRunningSamples','ResidualAluminium') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD ResidualAluminium REAL NOT NULL CONSTRAINT DF_ScadaRunningSamples_Residual DEFAULT 0;
IF COL_LENGTH('dbo.ScadaRunningSamples','SetAluminiumTemperature') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD SetAluminiumTemperature REAL NOT NULL CONSTRAINT DF_ScadaRunningSamples_SetAluminium DEFAULT 0;
IF COL_LENGTH('dbo.ScadaRunningSamples','SetFurnaceTemperature') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD SetFurnaceTemperature REAL NOT NULL CONSTRAINT DF_ScadaRunningSamples_SetFurnace DEFAULT 0;
IF COL_LENGTH('dbo.ScadaRunningSamples','CoolingActualFlowsJson') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD CoolingActualFlowsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ScadaRunningSamples_ActualFlows DEFAULT '[]';
IF COL_LENGTH('dbo.ScadaRunningSamples','CoolingSetFlowsJson') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD CoolingSetFlowsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ScadaRunningSamples_SetFlows DEFAULT '[]';
IF COL_LENGTH('dbo.ScadaRunningSamples','FullSnapshotJson') IS NULL ALTER TABLE dbo.ScadaRunningSamples ADD FullSnapshotJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ScadaRunningSamples_FullSnapshot DEFAULT '{}';
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaRunningSamples_DeviceTime' AND object_id=OBJECT_ID('dbo.ScadaRunningSamples'))
 CREATE INDEX IX_ScadaRunningSamples_DeviceTime ON dbo.ScadaRunningSamples(DeviceName,SampleTime DESC);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaRunningSamples_CycleId' AND object_id=OBJECT_ID('dbo.ScadaRunningSamples'))
 CREATE INDEX IX_ScadaRunningSamples_CycleId ON dbo.ScadaRunningSamples(CycleId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_ScadaRunningSamples_RecordId' AND object_id=OBJECT_ID('dbo.ScadaRunningSamples'))
 CREATE UNIQUE INDEX UX_ScadaRunningSamples_RecordId ON dbo.ScadaRunningSamples(RecordId) WHERE RecordId IS NOT NULL;
IF OBJECT_ID('dbo.ScadaProductionCycles','U') IS NULL CREATE TABLE dbo.ScadaProductionCycles(CycleId UNIQUEIDENTIFIER PRIMARY KEY,DeviceName NVARCHAR(100) NOT NULL,StartTime DATETIME2(3) NOT NULL,EndTime DATETIME2(3) NULL,StartReason NVARCHAR(200) NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaProductionCycles_DeviceTime' AND object_id=OBJECT_ID('dbo.ScadaProductionCycles'))
 CREATE INDEX IX_ScadaProductionCycles_DeviceTime ON dbo.ScadaProductionCycles(DeviceName,StartTime DESC);
IF OBJECT_ID('dbo.ScadaProcessSnapshots','U') IS NULL CREATE TABLE dbo.ScadaProcessSnapshots(Id BIGINT IDENTITY PRIMARY KEY,SavedAt DATETIME2 NOT NULL,DeviceName NVARCHAR(100) NOT NULL,Source NVARCHAR(200) NOT NULL,ProcessJson NVARCHAR(MAX) NOT NULL);
IF OBJECT_ID('dbo.ScadaProcessHistory','U') IS NULL CREATE TABLE dbo.ScadaProcessHistory(
 Id BIGINT IDENTITY PRIMARY KEY,SnapshotGroupId UNIQUEIDENTIFIER NOT NULL,SavedAt DATETIME2(3) NOT NULL,DeviceName NVARCHAR(100) NOT NULL,Source NVARCHAR(200) NOT NULL,ChannelNumber INT NOT NULL,
 Pressure1 REAL,Pressure2 REAL,Pressure3 REAL,Pressure4 REAL,Pressure5 REAL,Pressure6 REAL,Pressure7 REAL,Pressure8 REAL,MaintainPressure REAL,
 Time1 INT,Time2 INT,Time3 INT,Time4 INT,Time5 INT,Time6 INT,Time7 INT,Time8 INT,MaintainTime INT,
 CoolingTimeMax INT,CoolingTimeMin INT,CastingOpenTemperature REAL,CastingOpenMode INT,CastingOpenThermocoupleNm INT,
 AirAuMode INT,AirThermocouple INT,AirTemperature REAL,AirFlowSet REAL,AirControlMode INT,AirDelayTime INT,AirOpenTime INT,AirCrossTime INT,
 WaterAuMode INT,WaterThermocouple INT,WaterTemperature REAL,WaterFlowSet REAL,WaterControlMode INT,WaterDelayTime INT,WaterOpenTime INT,WaterCrossTime INT);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaProcessHistory_DeviceTime' AND object_id=OBJECT_ID('dbo.ScadaProcessHistory'))
 CREATE INDEX IX_ScadaProcessHistory_DeviceTime ON dbo.ScadaProcessHistory(DeviceName,SavedAt DESC);
IF OBJECT_ID('dbo.ScadaProcessHistorySummary','U') IS NULL CREATE TABLE dbo.ScadaProcessHistorySummary(
 Id BIGINT IDENTITY PRIMARY KEY,SavedAt DATETIME2(3) NOT NULL,DeviceName NVARCHAR(100) NOT NULL,Source NVARCHAR(200) NOT NULL,
 Pressure1 REAL,Pressure2 REAL,Pressure3 REAL,Pressure4 REAL,Pressure5 REAL,Pressure6 REAL,Pressure7 REAL,Pressure8 REAL,MaintainPressure REAL,
 Time1 INT,Time2 INT,Time3 INT,Time4 INT,Time5 INT,Time6 INT,Time7 INT,Time8 INT,MaintainTime INT,
 CoolingTimeMax INT,CoolingTimeMin INT,CastingOpenTemperature REAL,CastingOpenMode INT,CastingOpenThermocoupleNm INT,
 AirAuModesJson NVARCHAR(MAX),WaterAuModesJson NVARCHAR(MAX),AirThermocouplesJson NVARCHAR(MAX),WaterThermocouplesJson NVARCHAR(MAX),
 AirTemperaturesJson NVARCHAR(MAX),WaterTemperaturesJson NVARCHAR(MAX),AirFlowSetsJson NVARCHAR(MAX),WaterFlowSetsJson NVARCHAR(MAX),
 AirControlModesJson NVARCHAR(MAX),WaterControlModesJson NVARCHAR(MAX),AirDelayTimesJson NVARCHAR(MAX),WaterDelayTimesJson NVARCHAR(MAX),
 AirOpenTimesJson NVARCHAR(MAX),WaterOpenTimesJson NVARCHAR(MAX),AirCrossTimesJson NVARCHAR(MAX),WaterCrossTimesJson NVARCHAR(MAX));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaProcessHistorySummary_DeviceTime' AND object_id=OBJECT_ID('dbo.ScadaProcessHistorySummary'))
 CREATE INDEX IX_ScadaProcessHistorySummary_DeviceTime ON dbo.ScadaProcessHistorySummary(DeviceName,SavedAt DESC);
IF OBJECT_ID('dbo.ScadaAlarmEvents','U') IS NULL CREATE TABLE dbo.ScadaAlarmEvents(Id BIGINT IDENTITY PRIMARY KEY,OccurredAt DATETIME2 NOT NULL,RecoveredAt DATETIME2 NULL,DeviceName NVARCHAR(100) NOT NULL,Code NVARCHAR(100) NOT NULL,Level NVARCHAR(20) NOT NULL,Message NVARCHAR(500) NOT NULL,CurrentValue NVARCHAR(500) NOT NULL,IsActive BIT NOT NULL,IsAcknowledged BIT NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_ScadaAlarmEvents_DeviceActive' AND object_id=OBJECT_ID('dbo.ScadaAlarmEvents'))
 CREATE INDEX IX_ScadaAlarmEvents_DeviceActive ON dbo.ScadaAlarmEvents(DeviceName,IsActive,OccurredAt DESC);
IF OBJECT_ID('dbo.ScadaMolds','U') IS NULL CREATE TABLE dbo.ScadaMolds(Id BIGINT IDENTITY PRIMARY KEY,MoldNumber NVARCHAR(100) NOT NULL UNIQUE,ProductName NVARCHAR(200) NOT NULL,DrawingNumber NVARCHAR(200) NOT NULL,Remark NVARCHAR(1000) NOT NULL,UpdatedAt DATETIME2 NOT NULL);");
   Execute(@"IF OBJECT_ID('dbo.ScadaSyncReceipts','U') IS NULL
CREATE TABLE dbo.ScadaSyncReceipts(OperationId NVARCHAR(36) PRIMARY KEY,EntityType NVARCHAR(50) NOT NULL,SyncedAt DATETIME2(3) NOT NULL);");
   IsAvailable=true;
   message="SQL Server连接及数据表检查成功";
   LastStatusMessage=message;
   return true;
  }
  catch(Exception ex)
  {
   IsAvailable=false;
   message=GetFriendlyError(ex);
   LastStatusMessage=message;
   return false;
  }
 }
 public bool TryEnableWriting(out string message)
 {
  IsWritingEnabled=false;
  if(!TryInitialize(out message))return false;
  IsWritingEnabled=true;
  message="SQL Server已连接，远程写入已开启";
  LastStatusMessage=message;
  return true;
 }
 public void DisableWriting()
 {
  IsWritingEnabled=false;
  IsAvailable=false;
  LastStatusMessage="远程写入已由操作员停止";
 }
 public void SaveRunningSample(string device,PlcRuntimeSnapshot s,string? cycleId=null,string? recordId=null)
 {
  if(!CanWrite||!s.IsSuccess)return;
  int?[] actualFlows=s.CoolingChannels.SelectMany(x=>new int?[]{x.AirActual,x.WaterActual}).ToArray();
  int[] setFlows=s.CoolingChannels.SelectMany(x=>new[]{x.AirFlowSet,x.WaterFlowSet}).ToArray();
  Execute(@"IF @record IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ScadaRunningSamples WHERE RecordId=@record)
INSERT INTO dbo.ScadaRunningSamples
(RecordId,SampleTime,DeviceName,CycleId,ActualPressure,ResidualAluminium,AluminiumTemperature,SetAluminiumTemperature,FurnaceTemperature,SetFurnaceTemperature,TemperaturesJson,CoolingActualFlowsJson,CoolingSetFlowsJson,FullSnapshotJson)
VALUES(@record,@t,@d,@cycle,@p,@residual,@a,@setA,@f,@setF,@temperatures,@actualFlows,@setFlows,@snapshot)",
   ("@record",(object?)recordId??DBNull.Value),("@t",s.SampleTime),("@d",device),("@cycle",string.IsNullOrWhiteSpace(cycleId)?DBNull.Value:Guid.Parse(cycleId)),
   ("@p",s.ActualPressure),("@residual",s.AmountOfResidualAluminum),
   ("@a",s.ActualAluminiumTemperature),("@setA",s.SetAluminiumTemperature),
   ("@f",s.ActualFurnaceTemperature),("@setF",s.SetFurnaceTemperature),
   ("@temperatures",JsonSerializer.Serialize(s.Temperatures)),
   ("@actualFlows",JsonSerializer.Serialize(actualFlows)),("@setFlows",JsonSerializer.Serialize(setFlows)),
   ("@snapshot",JsonSerializer.Serialize(s)));
 }
 public void StartProductionCycle(string cycleId,string device,DateTime startTime,string startReason)
 {
  if(!CanWrite)return;
  Execute(@"IF NOT EXISTS(SELECT 1 FROM dbo.ScadaProductionCycles WHERE CycleId=@id)
INSERT INTO dbo.ScadaProductionCycles(CycleId,DeviceName,StartTime,StartReason) VALUES(@id,@device,@start,@reason)",
   ("@id",Guid.Parse(cycleId)),("@device",device),("@start",startTime),("@reason",startReason));
 }
 public void CompleteProductionCycle(string cycleId,DateTime endTime)
 {
  if(!CanWrite)return;
  Execute("UPDATE dbo.ScadaProductionCycles SET EndTime=@end WHERE CycleId=@id AND EndTime IS NULL",
   ("@end",endTime),("@id",Guid.Parse(cycleId)));
 }
 public void DeleteRunningSamplesOlderThan(DateTime cutoff)
 {
  if(!CanWrite)return;
  Execute("DELETE FROM dbo.ScadaRunningSamples WHERE SampleTime < @cutoff",("@cutoff",cutoff));
 }
 public void SaveProcessSnapshot(string device,string source,DieCastingProcess process)
  => SaveProcessSnapshotJson(device,source,JsonSerializer.Serialize(process));
 public void SaveProcessSnapshotJson(string device,string source,string processJson)
 {
  if(!CanWrite)return;
  Execute("INSERT INTO dbo.ScadaProcessSnapshots(SavedAt,DeviceName,Source,ProcessJson) VALUES(@t,@d,@s,@j)",("@t",DateTime.Now),("@d",device),("@s",source),("@j",processJson));
 }
 public void SaveProcessHistory(string device,string source,PlcRuntimeSnapshot snapshot)
 {
  if(!CanWrite)return;
  Guid groupId=Guid.NewGuid();
  DateTime savedAt=DateTime.Now;
  const string sql=@"INSERT INTO dbo.ScadaProcessHistory(
SnapshotGroupId,SavedAt,DeviceName,Source,ChannelNumber,
Pressure1,Pressure2,Pressure3,Pressure4,Pressure5,Pressure6,Pressure7,Pressure8,MaintainPressure,
Time1,Time2,Time3,Time4,Time5,Time6,Time7,Time8,MaintainTime,
CoolingTimeMax,CoolingTimeMin,CastingOpenTemperature,CastingOpenMode,CastingOpenThermocoupleNm,
AirAuMode,AirThermocouple,AirTemperature,AirFlowSet,AirControlMode,AirDelayTime,AirOpenTime,AirCrossTime,
WaterAuMode,WaterThermocouple,WaterTemperature,WaterFlowSet,WaterControlMode,WaterDelayTime,WaterOpenTime,WaterCrossTime)
VALUES(@group,@saved,@device,@source,@channel,
@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@pm,@t1,@t2,@t3,@t4,@t5,@t6,@t7,@t8,@tm,
@coolMax,@coolMin,@openTemp,@openMode,@openTc,
@airAu,@airTc,@airTemp,@airFlow,@airMode,@airDelay,@airOpen,@airCross,
@waterAu,@waterTc,@waterTemp,@waterFlow,@waterMode,@waterDelay,@waterOpen,@waterCross)";
  foreach(CoolingChannelItem channel in snapshot.ProcessCoolingChannels)
  {
   Execute(sql,
    ("@group",groupId),("@saved",savedAt),("@device",device),("@source",source),("@channel",channel.Channel),
    ("@p1",At(snapshot.PressureValues,0)),("@p2",At(snapshot.PressureValues,1)),("@p3",At(snapshot.PressureValues,2)),
    ("@p4",At(snapshot.PressureValues,3)),("@p5",At(snapshot.PressureValues,4)),("@p6",At(snapshot.PressureValues,5)),
    ("@p7",At(snapshot.PressureValues,6)),("@p8",At(snapshot.PressureValues,7)),("@pm",At(snapshot.PressureValues,8)),
    ("@t1",At(snapshot.TimeValues,0)),("@t2",At(snapshot.TimeValues,1)),("@t3",At(snapshot.TimeValues,2)),
    ("@t4",At(snapshot.TimeValues,3)),("@t5",At(snapshot.TimeValues,4)),("@t6",At(snapshot.TimeValues,5)),
    ("@t7",At(snapshot.TimeValues,6)),("@t8",At(snapshot.TimeValues,7)),("@tm",At(snapshot.TimeValues,8)),
    ("@coolMax",snapshot.CoolingTimeMax),("@coolMin",snapshot.CoolingTimeMin),("@openTemp",snapshot.CastingOpenTemperature),
    ("@openMode",snapshot.CastingOpenMode),("@openTc",snapshot.CastingOpenThermocoupleNm),
    ("@airAu",channel.AirAuMode),("@airTc",channel.AirTheNm),("@airTemp",channel.AirTemp),("@airFlow",channel.AirFlowSet),
    ("@airMode",channel.AirConMode),("@airDelay",channel.AirDelayTime),("@airOpen",channel.AirOpenTime),("@airCross",channel.AirCrossTime),
    ("@waterAu",channel.WaterAuMode),("@waterTc",channel.WaterTheNm),("@waterTemp",channel.WaterTemp),("@waterFlow",channel.WaterFlowSet),
    ("@waterMode",channel.WaterConMode),("@waterDelay",channel.WaterDelayTime),("@waterOpen",channel.WaterOpenTime),("@waterCross",channel.WaterCrossTime));
  }
 }
 public void SaveProcessHistorySummary(string device,string source,PlcRuntimeSnapshot snapshot)
 {
  if(!CanWrite)return;
  var c=snapshot.ProcessCoolingChannels;
  Execute(@"INSERT INTO dbo.ScadaProcessHistorySummary(
SavedAt,DeviceName,Source,Pressure1,Pressure2,Pressure3,Pressure4,Pressure5,Pressure6,Pressure7,Pressure8,MaintainPressure,
Time1,Time2,Time3,Time4,Time5,Time6,Time7,Time8,MaintainTime,CoolingTimeMax,CoolingTimeMin,CastingOpenTemperature,CastingOpenMode,CastingOpenThermocoupleNm,
AirAuModesJson,WaterAuModesJson,AirThermocouplesJson,WaterThermocouplesJson,AirTemperaturesJson,WaterTemperaturesJson,AirFlowSetsJson,WaterFlowSetsJson,
AirControlModesJson,WaterControlModesJson,AirDelayTimesJson,WaterDelayTimesJson,AirOpenTimesJson,WaterOpenTimesJson,AirCrossTimesJson,WaterCrossTimesJson)
VALUES(@saved,@device,@source,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@pm,@t1,@t2,@t3,@t4,@t5,@t6,@t7,@t8,@tm,@coolMax,@coolMin,@openTemp,@openMode,@openTc,
@airAu,@waterAu,@airTc,@waterTc,@airTemp,@waterTemp,@airFlow,@waterFlow,@airMode,@waterMode,@airDelay,@waterDelay,@airOpen,@waterOpen,@airCross,@waterCross)",
   ("@saved",DateTime.Now),("@device",device),("@source",source),
   ("@p1",At(snapshot.PressureValues,0)),("@p2",At(snapshot.PressureValues,1)),("@p3",At(snapshot.PressureValues,2)),("@p4",At(snapshot.PressureValues,3)),
   ("@p5",At(snapshot.PressureValues,4)),("@p6",At(snapshot.PressureValues,5)),("@p7",At(snapshot.PressureValues,6)),("@p8",At(snapshot.PressureValues,7)),("@pm",At(snapshot.PressureValues,8)),
   ("@t1",At(snapshot.TimeValues,0)),("@t2",At(snapshot.TimeValues,1)),("@t3",At(snapshot.TimeValues,2)),("@t4",At(snapshot.TimeValues,3)),
   ("@t5",At(snapshot.TimeValues,4)),("@t6",At(snapshot.TimeValues,5)),("@t7",At(snapshot.TimeValues,6)),("@t8",At(snapshot.TimeValues,7)),("@tm",At(snapshot.TimeValues,8)),
   ("@coolMax",snapshot.CoolingTimeMax),("@coolMin",snapshot.CoolingTimeMin),("@openTemp",snapshot.CastingOpenTemperature),
   ("@openMode",snapshot.CastingOpenMode),("@openTc",snapshot.CastingOpenThermocoupleNm),
   ("@airAu",JsonSerializer.Serialize(c.Select(x=>x.AirAuMode))),("@waterAu",JsonSerializer.Serialize(c.Select(x=>x.WaterAuMode))),
   ("@airTc",JsonSerializer.Serialize(c.Select(x=>x.AirTheNm))),("@waterTc",JsonSerializer.Serialize(c.Select(x=>x.WaterTheNm))),
   ("@airTemp",JsonSerializer.Serialize(c.Select(x=>x.AirTemp))),("@waterTemp",JsonSerializer.Serialize(c.Select(x=>x.WaterTemp))),
   ("@airFlow",JsonSerializer.Serialize(c.Select(x=>x.AirFlowSet))),("@waterFlow",JsonSerializer.Serialize(c.Select(x=>x.WaterFlowSet))),
   ("@airMode",JsonSerializer.Serialize(c.Select(x=>x.AirConMode))),("@waterMode",JsonSerializer.Serialize(c.Select(x=>x.WaterConMode))),
   ("@airDelay",JsonSerializer.Serialize(c.Select(x=>x.AirDelayTime))),("@waterDelay",JsonSerializer.Serialize(c.Select(x=>x.WaterDelayTime))),
   ("@airOpen",JsonSerializer.Serialize(c.Select(x=>x.AirOpenTime))),("@waterOpen",JsonSerializer.Serialize(c.Select(x=>x.WaterOpenTime))),
   ("@airCross",JsonSerializer.Serialize(c.Select(x=>x.AirCrossTime))),("@waterCross",JsonSerializer.Serialize(c.Select(x=>x.WaterCrossTime))));
 }
 public void UpsertAlarm(AlarmRecord a)
 {
  if(!CanWrite)return;
  Execute(@"IF EXISTS(SELECT 1 FROM dbo.ScadaAlarmEvents WHERE DeviceName=@d AND Code=@c AND OccurredAt=@o)
UPDATE dbo.ScadaAlarmEvents SET RecoveredAt=@r,CurrentValue=@v,IsActive=@active,IsAcknowledged=@ack WHERE DeviceName=@d AND Code=@c AND OccurredAt=@o
ELSE INSERT INTO dbo.ScadaAlarmEvents(OccurredAt,RecoveredAt,DeviceName,Code,Level,Message,CurrentValue,IsActive,IsAcknowledged) VALUES(@o,@r,@d,@c,@l,@m,@v,@active,@ack)",
   ("@o",a.OccurredAt),("@r",(object?)a.RecoveredAt??DBNull.Value),("@d",a.DeviceName),("@c",a.Code),("@l",a.Level),("@m",a.Message),("@v",a.CurrentValue),("@active",a.IsActive),("@ack",a.IsAcknowledged));
 }
 public void SaveMold(MoldRecord m)
 {
  if(!CanWrite)return;
  Execute(@"IF EXISTS(SELECT 1 FROM dbo.ScadaMolds WHERE MoldNumber=@n)
UPDATE dbo.ScadaMolds SET ProductName=@p,DrawingNumber=@d,Remark=@r,UpdatedAt=@t WHERE MoldNumber=@n
ELSE INSERT INTO dbo.ScadaMolds(MoldNumber,ProductName,DrawingNumber,Remark,UpdatedAt) VALUES(@n,@p,@d,@r,@t)",("@n",m.MoldNumber.Trim()),("@p",m.ProductName.Trim()),("@d",m.DrawingNumber.Trim()),("@r",m.Remark.Trim()),("@t",DateTime.Now));
 }
 public void DeleteMold(string moldNumber){if(CanWrite)Execute("DELETE FROM dbo.ScadaMolds WHERE MoldNumber=@n",("@n",moldNumber));}
 public bool HasSyncReceipt(string operationId)
 {
  if(!CanWrite)throw new InvalidOperationException("SQL Server远程写入当前未开启");
  lock(_sync)
  {
   if(_transactionConnection!=null)
   {
    using var transactionCommand=new SqlCommand("SELECT COUNT(*) FROM dbo.ScadaSyncReceipts WHERE OperationId=@id",_transactionConnection,_transaction){CommandTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
    transactionCommand.Parameters.AddWithValue("@id",operationId);
    return Convert.ToInt32(transactionCommand.ExecuteScalar())>0;
   }
   var builder=new SqlConnectionStringBuilder(_settings.SqlServerConnectionString){ConnectTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
   using var connection=new SqlConnection(builder.ConnectionString);connection.Open();
   using var command=new SqlCommand("SELECT COUNT(*) FROM dbo.ScadaSyncReceipts WHERE OperationId=@id",connection){CommandTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
   command.Parameters.AddWithValue("@id",operationId);
   return Convert.ToInt32(command.ExecuteScalar())>0;
  }
 }
 public void ApplySyncOperation(SyncOutboxItem item)
 {
  if(!CanWrite)throw new InvalidOperationException("SQL Server远程写入当前未开启");
  lock(_sync)
  {
   var builder=new SqlConnectionStringBuilder(_settings.SqlServerConnectionString){ConnectTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
   using var connection=new SqlConnection(builder.ConnectionString);connection.Open();
   using var transaction=connection.BeginTransaction();
   _transactionConnection=connection;_transaction=transaction;
   try
   {
    if(!HasSyncReceipt(item.Id)){DispatchSyncOperation(item);MarkSyncReceipt(item.Id,item.EntityType);}
    transaction.Commit();
   }
   catch(Exception ex)
   {
    try{transaction.Rollback();}catch{}
    IsAvailable=false;LastStatusMessage=GetFriendlyError(ex);throw;
   }
   finally{_transaction=null;_transactionConnection=null;}
  }
 }
 private void DispatchSyncOperation(SyncOutboxItem item)
 {
  var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
  switch(item.EntityType)
  {
   case SyncEntityTypes.Alarm:UpsertAlarm(Read<AlarmSyncPayload>(item,options).Alarm);break;
   case SyncEntityTypes.ProcessSnapshot:
    var ps=Read<ProcessSnapshotSyncPayload>(item,options);SaveProcessSnapshotJson(ps.DeviceName,ps.Source,ps.ProcessJson);break;
   case SyncEntityTypes.ProcessHistory:
    var ph=Read<ProcessHistorySyncPayload>(item,options);SaveProcessHistorySummary(ph.DeviceName,ph.Source,ph.Snapshot);break;
   case SyncEntityTypes.CycleStart:
    var cs=Read<CycleStartSyncPayload>(item,options);StartProductionCycle(cs.CycleId,cs.DeviceName,cs.StartTime,cs.Reason);break;
   case SyncEntityTypes.CycleComplete:
    var cc=Read<CycleCompleteSyncPayload>(item,options);CompleteProductionCycle(cc.CycleId,cc.EndTime);break;
   case SyncEntityTypes.MoldSave:SaveMold(Read<MoldSaveSyncPayload>(item,options).Mold);break;
   case SyncEntityTypes.MoldDelete:DeleteMold(Read<MoldDeleteSyncPayload>(item,options).MoldNumber);break;
   default:throw new InvalidDataException($"未知同步类型：{item.EntityType}");
  }
 }
 private static T Read<T>(SyncOutboxItem item,JsonSerializerOptions options) where T:class =>
  JsonSerializer.Deserialize<T>(item.PayloadJson,options)??throw new InvalidDataException($"{item.EntityType}同步内容为空");
 public void MarkSyncReceipt(string operationId,string entityType)
 {
  if(!CanWrite)throw new InvalidOperationException("SQL Server远程写入已在同步过程中停止");
  Execute(@"IF NOT EXISTS(SELECT 1 FROM dbo.ScadaSyncReceipts WHERE OperationId=@id)
INSERT INTO dbo.ScadaSyncReceipts(OperationId,EntityType,SyncedAt) VALUES(@id,@type,@time)",
   ("@id",operationId),("@type",entityType),("@time",DateTime.Now));
 }
 private void Execute(string sql,params (string Name,object Value)[] values)
 {
  lock(_sync)
  {
   try
   {
    if(_transactionConnection!=null)
    {
     using var transactionCommand=new SqlCommand(sql,_transactionConnection,_transaction){CommandTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
     foreach(var item in values)transactionCommand.Parameters.AddWithValue(item.Name,item.Value??DBNull.Value);
     transactionCommand.ExecuteNonQuery();return;
    }
    var builder=new SqlConnectionStringBuilder(_settings.SqlServerConnectionString){ConnectTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
    using var connection=new SqlConnection(builder.ConnectionString);connection.Open();
    using var command=new SqlCommand(sql,connection){CommandTimeout=Math.Max(1,_settings.CommandTimeoutSeconds)};
    foreach(var item in values)command.Parameters.AddWithValue(item.Name,item.Value??DBNull.Value);
    command.ExecuteNonQuery();
   }
   catch(Exception ex)
   {
    IsAvailable=false;
    LastStatusMessage=GetFriendlyError(ex);
    throw;
   }
  }
 }
 private static string GetFriendlyError(Exception ex)
 {
  if(ex is SqlException sqlEx)
  {
   return sqlEx.Number switch
   {
    -1 or 53 => "找不到 SQL Server。请检查服务是否启动、服务器名称和网络连接。",
    18456 => "SQL Server 登录失败。请检查登录方式、账号和权限。",
    4060 => "无法打开目标数据库。请先创建配置中指定的数据库。",
    _ => $"SQL Server 错误 {sqlEx.Number}：{sqlEx.Message}"
   };
  }
  return ex.Message;
 }
 private static float At(float[] values,int index)=>index<values.Length?values[index]:0f;
 private static int At(int[] values,int index)=>index<values.Length?values[index]:0;
}

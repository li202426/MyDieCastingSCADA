using MySCADA.Models;
using MySCADA.Services;
namespace MyDieCastingSCADA.Services;
public sealed class ProductionCycleService
{
    private readonly string _deviceName;
    private readonly ScadaDatabaseService _localDatabase;
    private readonly (string CycleId, DateTime StartTime)? _openCycleAtStartup;
    private bool _hasReceivedFirstSnapshot;
    private bool _lastInjectionRun;
    private string? _currentCycleId;
    public ProductionCycleService(
        string deviceName,
        ScadaDatabaseService localDatabase)
    {
        _deviceName = deviceName;
        _localDatabase = localDatabase;
        _openCycleAtStartup = _localDatabase.GetOpenProductionCycle(deviceName);
    }
        public void ProcessSnapshot(PlcRuntimeSnapshot snapshot)
    {
        if (!snapshot.IsSuccess) return;
        bool currentSignal = snapshot.InjuctionRun;
        if (!_hasReceivedFirstSnapshot)
        {
            RestoreInitialState(snapshot.SampleTime, currentSignal);
            _lastInjectionRun = currentSignal;
            _hasReceivedFirstSnapshot = true;
            snapshot.CycleId = _currentCycleId;
            return;
        }
        if (!_lastInjectionRun && currentSignal)
            StartCycle(snapshot.SampleTime, "InjuctionRun 上升沿");
        snapshot.CycleId = _currentCycleId;
        if (_lastInjectionRun && !currentSignal && _currentCycleId != null)
        {
            CompleteCycle(_currentCycleId, snapshot.SampleTime,
                "InjuctionRun 下降沿");
            _currentCycleId = null;
        }
        _lastInjectionRun = currentSignal;
    }
    private void RestoreInitialState(DateTime sampleTime, bool currentSignal)
    {
        if (_openCycleAtStartup is { } openCycle)
        {
            if (currentSignal)
            {
                _currentCycleId = openCycle.CycleId;
                _localDatabase.QueueSyncOperation(SyncEntityTypes.CycleStart,openCycle.CycleId,_deviceName,
                    new CycleStartSyncPayload{CycleId=openCycle.CycleId,DeviceName=_deviceName,StartTime=openCycle.StartTime,Reason="从本地数据库恢复的未结束周期"});
                OperationLogService.Info("压铸周期", "恢复未结束周期",
                    $"CycleId={openCycle.CycleId}；原开始时间={openCycle.StartTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                _localDatabase.QueueSyncOperation(SyncEntityTypes.CycleStart,openCycle.CycleId,_deviceName,
                    new CycleStartSyncPayload{CycleId=openCycle.CycleId,DeviceName=_deviceName,StartTime=openCycle.StartTime,Reason="从本地数据库补建的未结束周期"});
                CompleteCycle(openCycle.CycleId, sampleTime,
                    "软件重新启动后检测到设备已停止");
            }
            return;
        }
        if (currentSignal)
            StartCycle(sampleTime, "软件启动时检测到设备正在运行");
    }
    private void StartCycle(DateTime startTime, string reason)
    {
        _currentCycleId = Guid.NewGuid().ToString("D");
        SaveCycleStart(_currentCycleId, startTime, reason);
        OperationLogService.Success("压铸周期", "新周期开始",
            $"CycleId={_currentCycleId}；原因={reason}");
    }
    private void SaveCycleStart(string cycleId, DateTime startTime, string reason)
    {
        try
        {
            _localDatabase.StartProductionCycleWithSync(cycleId,_deviceName,startTime,reason);
        }
        catch (Exception ex)
        {
            OperationLogService.Error("压铸周期", "本地周期开始记录失败", ex.Message);
        }
    }
    private void CompleteCycle(string cycleId, DateTime endTime, string reason)
    {
        try
        {
            _localDatabase.CompleteProductionCycleWithSync(cycleId,_deviceName,endTime);
        }
        catch (Exception ex)
        {
            OperationLogService.Error("压铸周期", "本地周期结束记录失败", ex.Message);
        }
        OperationLogService.Success("压铸周期", "周期结束",
            $"CycleId={cycleId}；结束时间={endTime:yyyy-MM-dd HH:mm:ss}；原因={reason}");
    }
}

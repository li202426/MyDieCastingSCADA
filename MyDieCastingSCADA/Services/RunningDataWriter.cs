using MySCADA.Models;
using MySCADA.Services;
using System.Threading.Channels;
namespace MyDieCastingSCADA.Services;
public sealed class RunningDataWriter : IAsyncDisposable
{
    private readonly string _deviceName;
    private readonly ScadaDatabaseService _localDatabase;
    private readonly SqlServerDatabaseService _sqlServer;
    private readonly SqlSyncService _sqlSyncService;
    private readonly Channel<PlcRuntimeSnapshot> _queue;
    private readonly Task _worker;
    private DateTime _nextQueueWarningUtc = DateTime.MinValue;
    public RunningDataWriter(
        string deviceName,
        ScadaDatabaseService localDatabase,
        SqlServerDatabaseService sqlServer,
        SqlSyncService sqlSyncService,
        int capacity = 30)
    {
        _deviceName = deviceName;
        _localDatabase = localDatabase;
        _sqlServer = sqlServer;
        _sqlSyncService = sqlSyncService;
        _queue = Channel.CreateBounded<PlcRuntimeSnapshot>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _worker = Task.Run(ProcessQueueAsync);
    }
    public bool TryEnqueue(PlcRuntimeSnapshot snapshot)
    {
        if (_queue.Writer.TryWrite(snapshot)) return true;
        if (DateTime.UtcNow >= _nextQueueWarningUtc)
        {
            _nextQueueWarningUtc = DateTime.UtcNow.AddSeconds(30);
            OperationLogService.Warning(
                "运行数据存储",
                "写入队列已满",
                "数据库写入速度低于PLC采集速度，本次快照未入队；请检查磁盘或SQL Server连接。");
        }
        return false;
    }
    private async Task ProcessQueueAsync()
    {
        CleanupExpiredData();
        await foreach (PlcRuntimeSnapshot snapshot in _queue.Reader.ReadAllAsync())
        {
            try
            {
                string? recordId = _localDatabase.SaveRunningSample(
                    _deviceName, snapshot, snapshot.CycleId);
                if (recordId != null)
                    _sqlSyncService.NotifyNewData();
            }
            catch (Exception ex)
            {
                OperationLogService.Error("运行数据存储", "SQLite保存失败", ex.Message);
            }
        }
    }
    private void CleanupExpiredData()
    {
        int retentionDays = _sqlServer.RunningDataRetentionDays;
        if (retentionDays <= 0) return;
        DateTime cutoff = DateTime.Now.AddDays(-retentionDays);
        try
        {
            _localDatabase.DeleteRunningSamplesOlderThan(cutoff);
            _sqlServer.DeleteRunningSamplesOlderThan(cutoff);
            OperationLogService.Info(
                "运行数据清理", "清理完成", $"已清理 {cutoff:yyyy-MM-dd HH:mm:ss} 以前的实时采样");
        }
        catch (Exception ex)
        {
            OperationLogService.Warning("运行数据清理", "清理失败", ex.Message);
        }
    }
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }
}

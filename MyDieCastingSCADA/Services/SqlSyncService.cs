using MySCADA.Models;
using MySCADA.Services;
namespace MyDieCastingSCADA.Services;
public sealed class SqlSyncService : IAsyncDisposable
{
    private readonly ScadaDatabaseService _local;
    private readonly SqlServerDatabaseService _remote;
    private readonly CancellationTokenSource _cts=new();
    private readonly SemaphoreSlim _signal=new(0,1);
    private readonly Task _worker;
    private DateTime _nextReconnectUtc=DateTime.MinValue;
    private DateTime _nextFailureLogUtc=DateTime.MinValue;
    public string CurrentStatus { get; private set; }="SQL Server：写入已停止";
    public DateTime? LastConnectedAt { get; private set; }
    public DateTime? LastSuccessfulSyncAt { get; private set; }
    public DatabaseSyncCounts Counts => _local.GetSyncCounts();
    public event Action<string>? StatusChanged;
    public SqlSyncService(ScadaDatabaseService local,SqlServerDatabaseService remote)
    {
        _local=local;_remote=remote;PublishStatus();
        _worker=Task.Run(()=>RunAsync(_cts.Token));
    }
    public async Task<(bool Success,string Message)> EnableWritingAsync()
    {
        if(!AuthorizationService.Require(AppPermission.ManageDatabase,out string permissionError))return(false,permissionError);
        var result=await Task.Run(()=>
        {
            bool success=_remote.TryEnableWriting(out string message);
            return (success,message);
        }).ConfigureAwait(false);
        if(result.success)LastConnectedAt=DateTime.Now;
        NotifyNewData();
        return (result.success,result.message);
    }
    public void DisableWriting(){_remote.DisableWriting();PublishStatus();}
    public void NotifyNewData(){PublishStatus();if(_signal.CurrentCount==0)try{_signal.Release();}catch(ObjectDisposedException){}catch(SemaphoreFullException){}}
    public void RefreshStatus()=>NotifyNewData();
    public void RetryFailed(){_local.RetryFailedSyncOperations();NotifyNewData();}
    public IReadOnlyList<SyncOutboxItem> GetFailures(int count=500)=>_local.GetFailedSyncOperations(count)
        .Concat(_local.GetFailedRunningSampleOperations(count)).OrderByDescending(x=>x.CreatedAt).Take(count).ToList();
    private async Task RunAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            try{await _signal.WaitAsync(TimeSpan.FromSeconds(10),token).ConfigureAwait(false);await SynchronizeAsync(token).ConfigureAwait(false);}
            catch(OperationCanceledException) when(token.IsCancellationRequested){break;}
            catch(Exception ex){LogFailure("同步服务异常",ex.Message);await Task.Delay(5000,token).ConfigureAwait(false);}
        }
    }
    private Task SynchronizeAsync(CancellationToken token)
    {
        if(!_remote.IsEnabled||!_remote.IsWritingEnabled){PublishStatus();return Task.CompletedTask;}
        if(!_remote.IsAvailable)
        {
            if(DateTime.UtcNow<_nextReconnectUtc)return Task.CompletedTask;
            _nextReconnectUtc=DateTime.UtcNow.AddSeconds(30);
            if(!_remote.TryInitialize(out string message)){PublishStatus();LogFailure("SQL Server仍不可用",message);return Task.CompletedTask;}
            LastConnectedAt=DateTime.Now;
        }
        int success=SynchronizeRunningSamples(token)+SynchronizeBusinessOperations(token);
        if(success>0){LastSuccessfulSyncAt=DateTime.Now;OperationLogService.Success("数据库同步","同步完成",$"本次同步 {success} 条");}
        PublishStatus();return Task.CompletedTask;
    }
    private int SynchronizeRunningSamples(CancellationToken token)
    {
        int success=0;
        foreach(PendingRunningSample item in _local.GetPendingRunningSamples(100))
        {
            token.ThrowIfCancellationRequested();
            if(!item.Snapshot.IsSuccess){_local.MarkRunningSampleSyncFailed(item.RecordId,"FullSnapshotJson无法解析");continue;}
            try{_remote.SaveRunningSample(item.DeviceName,item.Snapshot,item.CycleId,item.RecordId);if(!_remote.CanWrite)throw new InvalidOperationException("远程写入已停止");_local.MarkRunningSampleSynced(item.RecordId);success++;}
            catch(Exception ex){_local.MarkRunningSampleSyncFailed(item.RecordId,ex.Message);LogFailure("实时数据同步暂停",ex.Message);break;}
        }
        return success;
    }
    private int SynchronizeBusinessOperations(CancellationToken token)
    {
        int success=0;
        foreach(SyncOutboxItem item in _local.GetPendingSyncOperations(100))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                _remote.ApplySyncOperation(item);
                _local.MarkSyncOperationSynced(item.Id);success++;
            }
            catch(Exception ex){_local.MarkSyncOperationFailed(item.Id,ex.Message);LogFailure($"{item.EntityType}同步暂停",ex.Message);break;}
        }
        return success;
    }
    private void LogFailure(string result,string detail)
    {
        if(DateTime.UtcNow<_nextFailureLogUtc)return;_nextFailureLogUtc=DateTime.UtcNow.AddSeconds(30);
        OperationLogService.Warning("数据库同步",result,detail);
    }
    private void PublishStatus()
    {
        DatabaseSyncCounts c=Counts;
        string time=LastSuccessfulSyncAt.HasValue?$" · 最后成功 {LastSuccessfulSyncAt:HH:mm:ss}":"";
        CurrentStatus=!_remote.IsEnabled?"SQL Server：配置未启用"
            :!_remote.IsWritingEnabled?$"SQL Server：写入已停止 · 待同步 {c.TotalWaiting} · 失败 {c.Failed} · 永久失败 {c.Dead}"
            :_remote.IsAvailable?$"SQL Server：已连接 · 待同步 {c.TotalWaiting} · 失败 {c.Failed} · 永久失败 {c.Dead}{time}"
            :$"SQL Server：正在重连 · 待同步 {c.TotalWaiting} · 失败 {c.Failed} · 永久失败 {c.Dead}";
        StatusChanged?.Invoke(CurrentStatus);
    }
    public async ValueTask DisposeAsync(){_cts.Cancel();try{await _worker.ConfigureAwait(false);}catch(OperationCanceledException){} _signal.Dispose();_cts.Dispose();}
}

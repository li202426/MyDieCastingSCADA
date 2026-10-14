using MyDieCastingSCADA.Services;
using MySCADA.Models;
using MySCADA.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
namespace MyDieCastingSCADA.UserControls;
public partial class SyncStatusPage:UserControl,INotifyPropertyChanged
{
    private readonly SqlSyncService _sync=DatabaseRuntime.Sync;
    public ObservableCollection<SyncOutboxItem> Failures { get; }=new();
    public string ConnectionState=>DatabaseRuntime.Remote.IsWritingEnabled?(DatabaseRuntime.Remote.IsAvailable?"已连接并写入":"正在重连"):"写入已停止";
    public int PendingCount=>_sync.Counts.Pending;
    public int FailedCount=>_sync.Counts.Failed;
    public int DeadCount=>_sync.Counts.Dead;
    public string TimeSummary=>$"连接：{Format(_sync.LastConnectedAt)}\n同步：{Format(_sync.LastSuccessfulSyncAt)}";
    public event PropertyChangedEventHandler? PropertyChanged;
    public SyncStatusPage()
    {
        InitializeComponent();DataContext=this;Loaded+=PageLoaded;Unloaded+=PageUnloaded;Refresh();
    }
    private void PageLoaded(object sender,RoutedEventArgs e)=>_sync.StatusChanged+=SyncStatusChanged;
    private void PageUnloaded(object sender,RoutedEventArgs e)=>_sync.StatusChanged-=SyncStatusChanged;
    private void SyncStatusChanged(string status)=>Dispatcher.BeginInvoke(new Action(Refresh));
    private void Refresh_Click(object sender,RoutedEventArgs e)=>Refresh();
    private void Retry_Click(object sender,RoutedEventArgs e)
    {
        if(!AuthorizationService.Require(AppPermission.ManageDatabase,out string error)){MessageBox.Show(error);return;}
        _sync.RetryFailed();OperationLogService.Info("数据库同步","失败任务已重新排队","后台将自动重试");Refresh();
    }
    private void Refresh()
    {
        Failures.Clear();foreach(SyncOutboxItem item in _sync.GetFailures())Failures.Add(item);
        OnPropertyChanged(nameof(ConnectionState));OnPropertyChanged(nameof(PendingCount));OnPropertyChanged(nameof(FailedCount));OnPropertyChanged(nameof(DeadCount));OnPropertyChanged(nameof(TimeSummary));
    }
    private static string Format(DateTime? value)=>value?.ToString("yyyy-MM-dd HH:mm:ss")??"尚无";
    private void OnPropertyChanged([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}

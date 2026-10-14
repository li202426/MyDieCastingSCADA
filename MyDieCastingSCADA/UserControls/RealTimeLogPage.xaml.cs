using MyDieCastingSCADA.Services;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace MyDieCastingSCADA.UserControls;
public partial class RealTimeLogPage:UserControl
{
 private readonly ObservableCollection<OperationLogEntry> displayLogs=new();
 public ICollectionView LogView{get;}
 public RealTimeLogPage(){InitializeComponent();Reload();LogView=CollectionViewSource.GetDefaultView(displayLogs);LogView.Filter=Matches;DataContext=this;OperationLogService.Entries.CollectionChanged+=LogsChanged;Unloaded+=PageUnloaded;}
 private void LogsChanged(object? sender,NotifyCollectionChangedEventArgs e){if(!DateFilter.SelectedDate.HasValue||DateFilter.SelectedDate.Value.Date==DateTime.Today)Reload();}
 private void PageUnloaded(object sender,RoutedEventArgs e){OperationLogService.Entries.CollectionChanged-=LogsChanged;Unloaded-=PageUnloaded;}
 private bool Matches(object value){if(value is not OperationLogEntry x)return false;var day=DateFilter.SelectedDate;var level=(LevelFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();var key=KeywordBox.Text?.Trim()??"";bool dateOk=!day.HasValue||x.Time.Date==day.Value.Date;bool levelOk=level=="全部等级"||string.IsNullOrEmpty(level)||x.Level==level;bool keyOk=key.Length==0||x.UserName.Contains(key,StringComparison.OrdinalIgnoreCase)||x.Operation.Contains(key,StringComparison.OrdinalIgnoreCase)||x.Result.Contains(key,StringComparison.OrdinalIgnoreCase)||x.Detail.Contains(key,StringComparison.OrdinalIgnoreCase);return dateOk&&levelOk&&keyOk;}
 private void FilterChanged(object sender,RoutedEventArgs e){Reload();LogView?.Refresh();}
 private void Reset_Click(object sender,RoutedEventArgs e){DateFilter.SelectedDate=null;LevelFilter.SelectedIndex=0;KeywordBox.Clear();LogView.Refresh();}
 private void ClearLog_Click(object sender,RoutedEventArgs e){OperationLogService.Clear();displayLogs.Clear();OperationLogService.Info("日志管理","当前显示已清空","磁盘历史日志文件仍然保留");}
 private void Reload(){displayLogs.Clear();var source=DateFilter?.SelectedDate is DateTime day?OperationLogService.ReadDate(day):OperationLogService.Entries;foreach(var x in source)displayLogs.Add(x);}
}

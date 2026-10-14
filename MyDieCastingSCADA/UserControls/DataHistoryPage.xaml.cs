using MyDieCastingSCADA.Services;
using MySCADA.Models;
using MySCADA.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
namespace MyDieCastingSCADA.UserControls;
public partial class DataHistoryPage:UserControl
{
 private readonly ScadaDatabaseService _db;
 private readonly SqlServerDatabaseService _sql;
 public ObservableCollection<StoredRunningSample> Samples{get;}=new();
 public DataHistoryPage(ScadaDatabaseService db,SqlServerDatabaseService sql)
 {
  InitializeComponent();
  _db=db;
  _sql=sql;
  DataContext=this;
  StartDatePicker.SelectedDate=DateTime.Today;
  EndDatePicker.SelectedDate=DateTime.Today;
  TestSqlButton.Visibility=AuthorizationService.Has(AppPermission.ManageDatabase)?Visibility.Visible:Visibility.Collapsed;
  UpdateDatabaseStatus();
  _=ReloadAsync();
 }
 private async void Refresh_Click(object sender,RoutedEventArgs e)=>await ReloadAsync();
 private async void Today_Click(object sender,RoutedEventArgs e)
 {
  StartDatePicker.SelectedDate=DateTime.Today;
  EndDatePicker.SelectedDate=DateTime.Today;
  await ReloadAsync();
 }
 private async void TestSqlServer_Click(object sender,RoutedEventArgs e)
 {
  if(!AuthorizationService.Require(AppPermission.ManageDatabase,out string error)){MessageBox.Show(error);return;}
  DatabaseStatus.Text="正在测试 SQL Server，请稍候...";
  bool success=await Task.Run(()=>_sql.TryInitialize(out _));
  UpdateDatabaseStatus();
  if(success)
   OperationLogService.Success("测试 SQL Server","连接成功",_sql.LastStatusMessage);
  else
   OperationLogService.Warning("测试 SQL Server","连接未成功",_sql.LastStatusMessage);
 }
 private async Task ReloadAsync()
 {
  DateTime? start=StartDatePicker.SelectedDate?.Date;
  DateTime? end=EndDatePicker.SelectedDate?.Date.AddDays(1);
  if(start.HasValue&&end.HasValue&&start>=end)
  {
   MessageBox.Show("开始日期不能晚于结束日期。","查询条件",MessageBoxButton.OK,MessageBoxImage.Information);
   return;
  }
  int count=200;
  if(CountBox.SelectedItem is ComboBoxItem item)
   int.TryParse(item.Content?.ToString(),out count);
  QueryStatus.Text="正在查询...";
  try
  {
   List<StoredRunningSample> rows=await Task.Run(()=>_db.GetRunningSamples(start,end,count));
   Samples.Clear();
   foreach(StoredRunningSample row in rows) Samples.Add(row);
   QueryStatus.Text=$"查询完成，共 {rows.Count} 条（数据来源：本地 SQLite）";
  }
  catch(Exception ex)
  {
   QueryStatus.Text="查询失败";
   OperationLogService.Error("查询运行数据","查询失败",ex.Message);
   MessageBox.Show($"查询运行数据失败：{ex.Message}","错误",MessageBoxButton.OK,MessageBoxImage.Error);
  }
 }
 private void UpdateDatabaseStatus()
 {
  string sqlState=!_sql.IsEnabled?"未启用":_sql.IsAvailable?"连接正常":"当前不可用";
  DatabaseStatus.Text=$"本地 SQLite：{_db.DatabasePath}\nSQL Server：{sqlState}。{_sql.LastStatusMessage}";
 }
}

using MyDieCastingSCADA.Services;using MySCADA.Models;using MySCADA.Services;using System.Collections.ObjectModel;using System.Windows;using System.Windows.Controls;
namespace MyDieCastingSCADA.UserControls;
public partial class MoldManagementPage:UserControl
{
 private readonly ScadaDatabaseService _db;private readonly SqlServerDatabaseService _sql;public ObservableCollection<MoldRecord>Molds{get;}=new();
 public MoldManagementPage(ScadaDatabaseService db,SqlServerDatabaseService sql){InitializeComponent();_db=db;_sql=sql;DataContext=this;Reload();}
 private void Reload(){Molds.Clear();foreach(var x in _db.GetMolds())Molds.Add(x);}
 private void Save_Click(object s,RoutedEventArgs e){if(!AuthorizationService.Require(AppPermission.ManageMolds,out string error)){MessageBox.Show(error);return;}if(string.IsNullOrWhiteSpace(MoldNumberBox.Text)){MessageBox.Show("请输入模具编号");return;}var m=new MoldRecord{MoldNumber=MoldNumberBox.Text,ProductName=ProductBox.Text,DrawingNumber=DrawingBox.Text,Remark=RemarkBox.Text};_db.SaveMoldWithSync(m);DatabaseRuntime.Sync.NotifyNewData();OperationLogService.Success("模具管理","保存完成",MoldNumberBox.Text);Reload();}
 private void Delete_Click(object s,RoutedEventArgs e){if(!AuthorizationService.Require(AppPermission.ManageMolds,out string error)){MessageBox.Show(error);return;}if(s is Button{Tag:MoldRecord m}&&MessageBox.Show($"确认删除模具 {m.MoldNumber}？","确认",MessageBoxButton.YesNo)==MessageBoxResult.Yes){_db.DeleteMoldWithSync(m.Id,m.MoldNumber);DatabaseRuntime.Sync.NotifyNewData();OperationLogService.Info("模具管理","删除完成",m.MoldNumber);Reload();}}
 private void SelectionChanged(object s,SelectionChangedEventArgs e){if(MoldGrid.SelectedItem is MoldRecord m){MoldNumberBox.Text=m.MoldNumber;ProductBox.Text=m.ProductName;DrawingBox.Text=m.DrawingNumber;RemarkBox.Text=m.Remark;}}
}

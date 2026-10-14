using MyDieCastingSCADA.Services;
using MySCADA.Models;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace MyDieCastingSCADA.UserControls;
public partial class AlarmPage:UserControl
{
 private readonly AlarmService service; public ICollectionView AlarmView{get;}
 public AlarmPage(AlarmService value){InitializeComponent();service=value;AlarmView=CollectionViewSource.GetDefaultView(value.Records);AlarmView.Filter=Matches;DataContext=this;value.Records.CollectionChanged+=RecordsChanged;Unloaded+=PageUnloaded;}
 private void RecordsChanged(object? sender,NotifyCollectionChangedEventArgs e)=>AlarmView.Refresh();
 private void PageUnloaded(object sender,RoutedEventArgs e){service.Records.CollectionChanged-=RecordsChanged;Unloaded-=PageUnloaded;}
 private bool Matches(object value){if(value is not AlarmRecord a)return false;var status=(StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();var level=(LevelFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();var key=KeywordBox.Text?.Trim()??"";return (status switch{"活动报警"=>a.IsActive,"已恢复"=>!a.IsActive,"未确认"=>!a.IsAcknowledged,_=>true})&&(level=="全部等级"||string.IsNullOrEmpty(level)||a.Level==level)&&(key.Length==0||a.Message.Contains(key,StringComparison.OrdinalIgnoreCase)||a.CurrentValue.Contains(key,StringComparison.OrdinalIgnoreCase));}
 private void FilterChanged(object sender,RoutedEventArgs e)=>AlarmView?.Refresh();
 private void Acknowledge_Click(object sender,RoutedEventArgs e){service.Acknowledge(AlarmGrid.SelectedItem as AlarmRecord);AlarmView.Refresh();}
}

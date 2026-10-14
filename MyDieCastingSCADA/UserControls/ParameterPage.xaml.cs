using MyDieCastingSCADA;
using MySCADA.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MyDieCastingSCADA.Services;
namespace MyDieCastingSCADA.UserControls
{
    public partial class ParameterPage : UserControl
    {
        private ViceWindow? _viceWindow;
        public ParameterPage()
        {
            InitializeComponent();
            Loaded += ParameterPage_Loaded;
        }
        private async void ParameterPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViceWindow vm)
            {
                _viceWindow = vm;
                this.DataContext = vm;
                cbRecipes.ItemsSource = GetRecipeFiles();
                OperationLogService.Info("打开工艺下发页面", "开始读取当前参数", "准备从 PLC 加载当前工艺数据");
                await LoadCurrentPLCToEditorAsync();
            }
        }
        private string[] GetRecipeFiles()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RECIPES");
            Directory.CreateDirectory(path);
            return Directory.GetFiles(path, "*.json").Select(Path.GetFileName).ToArray();
        }
        private async void ChannelOK_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EditableChannelItem item && _viceWindow != null)
            {
                string before=DescribeCurrentChannel(item.Channel);
                string after=DescribeChannel(item);
                string audit=$"修改前：{before}；修改后：{after}";
                OperationLogService.Info("单通道工艺下发", "开始执行", audit);
                IsEnabled = false;
                bool success;
                try
                {
                    success = await Task.Run(
                        () => _viceWindow.DownloadSingleChannel(item.Channel, item));
                }
                finally
                {
                    IsEnabled = true;
                }
                string message = success
                    ? $"通道 {item.Channel} 下发成功"
                    : _viceWindow.LastOperationError ?? $"通道 {item.Channel} 下发失败";
                if (success)
                {
                    SyncUIToDCP();
                    _viceWindow.SaveProcessSnapshot($"单通道{item.Channel}下发");
                    OperationLogService.Success("单通道工艺下发", "执行完成", audit);
                }
                else
                    OperationLogService.Error("单通道工艺下发", "执行失败", $"{audit}；错误：{message}");
                MessageBox.Show(message,
                    "提示", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        private async void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            if (_viceWindow == null) return;
            var confirm = MessageBox.Show(
                "即将依次写入全部冷却通道。确认继续吗？",
                "确认全部下发",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            string before=DescribeCurrentChannels();
            string after=DescribeAllChannels(_viceWindow.EditableChannels);
            string audit=$"修改前：{before}；修改后：{after}";
            OperationLogService.Info("全部通道工艺下发", "开始执行",audit);
            IsEnabled = false;
            bool success;
            try
            {
                success = await Task.Run(() => _viceWindow.DownloadAllChannels());
            }
            finally
            {
                IsEnabled = true;
            }
            string message = success
                ? "全部通道下发完成"
                : _viceWindow.LastOperationError ?? "部分通道下发失败";
            if (success)
            {
                SyncUIToDCP();
                _viceWindow.SaveProcessSnapshot("全部通道下发");
                OperationLogService.Success("全部通道工艺下发", "执行完成",audit);
            }
            else
                OperationLogService.Error("全部通道工艺下发", "执行失败", $"{audit}；错误：{message}");
            MessageBox.Show(message, "提示", MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        private void BtnLoadRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (cbRecipes.SelectedItem is not string fileName || _viceWindow == null)
            {
                MessageBox.Show("请先选择一个工艺文件！", "提示");
                return;
            }
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RECIPES", fileName);
            try
            {
                OperationLogService.Info("加载工艺文件", "开始读取", fileName);
                string json = File.ReadAllText(fullPath);
                var dcp = System.Text.Json.JsonSerializer.Deserialize<DieCastingProcess>(json);
                if (dcp == null)
                {
                    OperationLogService.Error("加载工艺文件", "读取失败", $"{fileName} 内容为空或格式错误");
                    MessageBox.Show("工艺文件内容为空或格式错误！", "错误");
                    return;
                }
                _viceWindow.DownLoadDCP = dcp;
                ApplyDCPToUI(dcp);
                _viceWindow.SaveProcessSnapshot($"加载工艺文件：{fileName}");
                OperationLogService.Success("加载工艺文件", "加载完成", fileName);
                MessageBox.Show($"✅ 工艺已成功加载：{fileName}", "加载成功");
            }
            catch (Exception ex)
            {
                OperationLogService.Error("加载工艺文件", "加载失败", ex.Message);
                MessageBox.Show($"加载工艺失败：\n{ex.Message}", "错误");
            }
        }
        private void BtnSaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (_viceWindow == null) return;
            try
            {
                OperationLogService.Info("保存工艺文件", "开始执行", "正在收集编辑区参数");
                SyncUIToDCP();
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RECIPES");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                string fileName = $"工艺_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string fullPath = Path.Combine(path, fileName);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                string json = System.Text.Json.JsonSerializer.Serialize(_viceWindow.DownLoadDCP, options);
                File.WriteAllText(fullPath, json);
                _viceWindow.SaveProcessSnapshot($"保存工艺文件：{fileName}");
                OperationLogService.Success("保存工艺文件", "保存完成", fileName);
                MessageBox.Show($"✅ 工艺已保存：{fileName}", "保存成功");
                cbRecipes.ItemsSource = GetRecipeFiles();
            }
            catch (Exception ex)
            {
                OperationLogService.Error("保存工艺文件", "保存失败", ex.Message);
                MessageBox.Show($"保存失败：{ex.Message}", "错误");
            }
        }
        private async Task RefreshDataFromPLCAsync()
        {
            if (_viceWindow == null) return;
            try
            {
                await _viceWindow.RefreshNowAsync();
            }
            catch (Exception ex)
            {
                OperationLogService.Error("刷新 PLC 工艺数据", "刷新失败", ex.Message);
                Console.WriteLine($"刷新 PLC 数据失败: {ex.Message}");
            }
        }
        private async Task LoadCurrentPLCToEditorAsync()
        {
            if (_viceWindow == null) return;
            try
            {
                await RefreshDataFromPLCAsync();
                for (int i = 0; i < _viceWindow.ProcessCoolingChannels.Count && i < _viceWindow.EditableChannels.Count; i++)
                {
                    var src = _viceWindow.ProcessCoolingChannels[i]; 
                    var dst = _viceWindow.EditableChannels[i];     
                    dst.AirSetTemp = src.AirTemp;
                    dst.AirFlow = src.AirFlowSet;
                    dst.AirLastTime = src.AirDelayTime;
                    dst.AirOpenTime = src.AirOpenTime;
                    dst.AirCrossTime = src.AirCrossTime;
                    dst.AirConMode = src.AirConMode;
                    dst.AirTheNm = src.AirTheNm;
                    dst.WaterSetTemp = src.WaterTemp;
                    dst.WaterFlow = src.WaterFlowSet;
                    dst.WaterLastTime = src.WaterDelayTime;
                    dst.WaterOpenTime = src.WaterOpenTime;
                    dst.WaterCrossTime = src.WaterCrossTime;
                    dst.WaterConMode = src.WaterConMode;
                    dst.WaterTheNm = src.WaterTheNm;
                }
                _viceWindow.EditableCastingOpenMode = (int)_viceWindow.CastingOpenModeItem.Value;
                _viceWindow.EditableCastingOpenTemp = _viceWindow.CastingOpenTempItem.Value;
                _viceWindow.EditableCastingOpenThermo = (int)_viceWindow.CastingOpenThermoItem.Value;
                _viceWindow.EditableMaintainTime = _viceWindow.TimeItems.Count > 8 ? _viceWindow.TimeItems[8].Value : 0;
                _viceWindow.EditableCoolingTimeMax = _viceWindow.CoolingTimeMaxItem.Value;
                _viceWindow.EditableCoolingTimeMin = _viceWindow.CoolingTimeMinItem.Value;
                var temp = _viceWindow.EditableChannels.ToList();
                _viceWindow.EditableChannels.Clear();
                foreach (var item in temp) _viceWindow.EditableChannels.Add(item);
                OperationLogService.Success("读取 PLC 当前工艺", "加载到编辑区完成",
                    $"已加载 {_viceWindow.EditableChannels.Count} 个冷却通道");
            }
            catch (Exception ex)
            {
                OperationLogService.Error("读取 PLC 当前工艺", "加载失败", ex.Message);
                Console.WriteLine($"加载当前工艺到编辑区失败: {ex.Message}");
            }
        }
        private void ApplyDCPToUI(DieCastingProcess dcp)
        {
            if (dcp == null || _viceWindow == null) return;
            try
            {
                for (int i = 0; i < 9 && i < _viceWindow.PressureItems.Count; i++)
                    _viceWindow.PressureItems[i].Value = dcp.PressureSet != null && i < dcp.PressureSet.Length ? dcp.PressureSet[i] : 0f;
                for (int i = 0; i < 9 && i < _viceWindow.TimeItems.Count; i++)
                    _viceWindow.TimeItems[i].Value = dcp.TimeSet != null && i < dcp.TimeSet.Length ? dcp.TimeSet[i] : 0;
                _viceWindow.EditableCastingOpenMode = dcp.CastingOpenMode;
                _viceWindow.EditableCastingOpenTemp = dcp.CastingOpenTemperature;
                _viceWindow.EditableCastingOpenThermo = dcp.CastingOpenThermocoupleNm;
                _viceWindow.EditableMaintainTime = dcp.TimeSet != null && dcp.TimeSet.Length > 8 ? dcp.TimeSet[8] : 0;
                _viceWindow.EditableCoolingTimeMax = dcp.CoolingTimeMax;
                _viceWindow.EditableCoolingTimeMin = dcp.CoolingTimeMin;
                for (int ch = 0; ch < 17 && ch < _viceWindow.EditableChannels.Count; ch++)
                {
                    int idx = ch * 2;
                    var editItem = _viceWindow.EditableChannels[ch];
                    if (dcp.CoolingAuMode != null && idx < dcp.CoolingAuMode.Length)
                    {
                        editItem.AirConMode = dcp.CoolingConMode != null && idx < dcp.CoolingConMode.Length ? dcp.CoolingConMode[idx] : 0;
                        editItem.AirTheNm = dcp.CoolingTheNm != null && idx < dcp.CoolingTheNm.Length ? dcp.CoolingTheNm[idx] : 0;
                        editItem.AirSetTemp = dcp.CoolingTemp != null && idx < dcp.CoolingTemp.Length ? dcp.CoolingTemp[idx] : 0f;
                        editItem.AirFlow = dcp.CoolingFlowSet != null && idx < dcp.CoolingFlowSet.Length ? (int)dcp.CoolingFlowSet[idx] : 0;
                        editItem.AirLastTime = dcp.CoolingDelayTime != null && idx < dcp.CoolingDelayTime.Length ? dcp.CoolingDelayTime[idx] : 0;
                        editItem.AirOpenTime = dcp.CoolingOpenTime != null && idx < dcp.CoolingOpenTime.Length ? dcp.CoolingOpenTime[idx] : 0;
                        editItem.AirCrossTime = dcp.CoolingCrossTime != null && idx < dcp.CoolingCrossTime.Length ? dcp.CoolingCrossTime[idx] : 0;
                        int wIdx = ch * 2 + 1;
                        editItem.WaterConMode = dcp.CoolingConMode != null && wIdx < dcp.CoolingConMode.Length ? dcp.CoolingConMode[wIdx] : 0;
                        editItem.WaterTheNm = dcp.CoolingTheNm != null && wIdx < dcp.CoolingTheNm.Length ? dcp.CoolingTheNm[wIdx] : 0;
                        editItem.WaterSetTemp = dcp.CoolingTemp != null && wIdx < dcp.CoolingTemp.Length ? dcp.CoolingTemp[wIdx] : 0f;
                        editItem.WaterFlow = dcp.CoolingFlowSet != null && wIdx < dcp.CoolingFlowSet.Length ? (int)dcp.CoolingFlowSet[wIdx] : 0;
                        editItem.WaterLastTime = dcp.CoolingDelayTime != null && wIdx < dcp.CoolingDelayTime.Length ? dcp.CoolingDelayTime[wIdx] : 0;
                        editItem.WaterOpenTime = dcp.CoolingOpenTime != null && wIdx < dcp.CoolingOpenTime.Length ? dcp.CoolingOpenTime[wIdx] : 0;
                        editItem.WaterCrossTime = dcp.CoolingCrossTime != null && wIdx < dcp.CoolingCrossTime.Length ? dcp.CoolingCrossTime[wIdx] : 0;
                    }
                }
                var temp = _viceWindow.EditableChannels.ToList();
                _viceWindow.EditableChannels.Clear();
                foreach (var item in temp) _viceWindow.EditableChannels.Add(item);
                MessageBox.Show("✅ 工艺已成功加载到编辑区！", "加载完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用工艺失败：{ex.Message}", "错误");
            }
        }
        private void SyncUIToDCP()
        {
            var dcp = _viceWindow.DownLoadDCP;
            if (dcp == null) return;
            for (int i = 0; i < 9; i++)
            {
                dcp.PressureSet[i] = _viceWindow.PressureItems[i].Value;
                dcp.TimeSet[i] = _viceWindow.TimeItems[i].Value;
            }
            dcp.CoolingTimeMax = (int)_viceWindow.EditableCoolingTimeMax;
            dcp.CoolingTimeMin = (int)_viceWindow.EditableCoolingTimeMin;
            dcp.CastingOpenTemperature = _viceWindow.EditableCastingOpenTemp;
            dcp.CastingOpenMode = (short)_viceWindow.EditableCastingOpenMode;
            dcp.CastingOpenThermocoupleNm = (short)_viceWindow.EditableCastingOpenThermo;
            for (int ch = 0; ch < 17 && ch < _viceWindow.EditableChannels.Count; ch++)
            {
                int idx = ch * 2;
                var item = _viceWindow.EditableChannels[ch];
                dcp.CoolingConMode[idx] = (short)item.AirConMode;
                dcp.CoolingTheNm[idx] = (short)item.AirTheNm;
                dcp.CoolingTemp[idx] = item.AirSetTemp;
                dcp.CoolingFlowSet[idx] = item.AirFlow;
                dcp.CoolingDelayTime[idx] = item.AirLastTime;
                dcp.CoolingOpenTime[idx] = item.AirOpenTime;
                dcp.CoolingCrossTime[idx] = item.AirCrossTime;
                int wIdx = ch * 2 + 1;
                dcp.CoolingConMode[wIdx] = (short)item.WaterConMode;
                dcp.CoolingTheNm[wIdx] = (short)item.WaterTheNm;
                dcp.CoolingTemp[wIdx] = item.WaterSetTemp;
                dcp.CoolingFlowSet[wIdx] = item.WaterFlow;
                dcp.CoolingDelayTime[wIdx] = item.WaterLastTime;
                dcp.CoolingOpenTime[wIdx] = item.WaterOpenTime;
                dcp.CoolingCrossTime[wIdx] = item.WaterCrossTime;
            }
        }
        private async void BtnDownloadOpenParam_Click(object sender, RoutedEventArgs e)
        {
            if (_viceWindow == null) return;
            string before=$"方式={(int)_viceWindow.CastingOpenModeItem.Value}，温度={_viceWindow.CastingOpenTempItem.Value}，热电偶={(int)_viceWindow.CastingOpenThermoItem.Value}";
            string after = $"方式={_viceWindow.EditableCastingOpenMode}，" +
                $"温度={_viceWindow.EditableCastingOpenTemp}，" +
                $"热电偶={_viceWindow.EditableCastingOpenThermo}";
            string auditDetail=$"修改前：{before}；修改后：{after}";
            OperationLogService.Info("开模参数下发", "开始执行", auditDetail);
            IsEnabled = false;
            bool success;
            try
            {
                success = await Task.Run(() => _viceWindow.DownloadOpenParam());
            }
            finally
            {
                IsEnabled = true;
            }
            if (success)
            {
                SyncUIToDCP();
                _viceWindow.SaveProcessSnapshot("开模参数下发");
                OperationLogService.Success("开模参数下发", "执行完成", auditDetail);
            }
            else
                OperationLogService.Error("开模参数下发", "执行失败",
                    $"{auditDetail}；错误：{_viceWindow.LastOperationError ?? "未返回详细错误信息"}");
            MessageBox.Show(
                success ? "开模参数下发成功" : _viceWindow.LastOperationError ?? "开模参数下发失败",
                "提示",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        private async void BtnDownloadMaintainCoolTime_Click(object sender, RoutedEventArgs e)
        {
            if (_viceWindow == null) return;
            string before=$"保压时间={(_viceWindow.TimeItems.Count>8?_viceWindow.TimeItems[8].Value:0)}，冷却Max={_viceWindow.CoolingTimeMaxItem.Value}，冷却Min={_viceWindow.CoolingTimeMinItem.Value}";
            string after = $"保压时间={_viceWindow.EditableMaintainTime}，" +
                $"冷却Max={_viceWindow.EditableCoolingTimeMax}，" +
                $"冷却Min={_viceWindow.EditableCoolingTimeMin}";
            string auditDetail=$"修改前：{before}；修改后：{after}";
            OperationLogService.Info("保压冷却时间下发", "开始执行", auditDetail);
            IsEnabled = false;
            bool success;
            try
            {
                success = await Task.Run(() => _viceWindow.DownloadMaintainCoolTime());
            }
            finally
            {
                IsEnabled = true;
            }
            if (success)
            {
                SyncUIToDCP();
                _viceWindow.SaveProcessSnapshot("保压冷却时间下发");
                OperationLogService.Success("保压冷却时间下发", "执行完成", auditDetail);
            }
            else
                OperationLogService.Error("保压冷却时间下发", "执行失败",
                    $"{auditDetail}；错误：{_viceWindow.LastOperationError ?? "未返回详细错误信息"}");
            MessageBox.Show(
                success ? "保压冷却时间下发成功" : _viceWindow.LastOperationError ?? "保压冷却时间下发失败",
                "提示",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        private static string DescribeChannel(EditableChannelItem item) =>
            $"通道={item.Channel}；" +
            $"风冷[模式={item.AirConMode},热偶={item.AirTheNm},温度={item.AirSetTemp}," +
            $"流量={item.AirFlow},延迟={item.AirLastTime},开启={item.AirOpenTime},交叉={item.AirCrossTime}]；" +
            $"水冷[模式={item.WaterConMode},热偶={item.WaterTheNm},温度={item.WaterSetTemp}," +
            $"流量={item.WaterFlow},延迟={item.WaterLastTime},开启={item.WaterOpenTime},交叉={item.WaterCrossTime}]";
        private static string DescribeAllChannels(
            System.Collections.Generic.IEnumerable<EditableChannelItem> channels)
        {
            EditableChannelItem[] items = channels.ToArray();
            return $"共{items.Length}个通道；" + string.Join(" | ", items.Select(DescribeChannel));
        }
        private string DescribeCurrentChannel(int channel)
        {
            if(_viceWindow==null)return "不可用";
            CoolingChannelItem? item=_viceWindow.ProcessCoolingChannels.FirstOrDefault(x=>x.Channel==channel);
            return item==null?"未读取":$"通道={channel}；风冷[模式={item.AirConMode},热偶={item.AirTheNm},温度={item.AirTemp},流量={item.AirFlowSet},延迟={item.AirDelayTime},开启={item.AirOpenTime},交叉={item.AirCrossTime}]；水冷[模式={item.WaterConMode},热偶={item.WaterTheNm},温度={item.WaterTemp},流量={item.WaterFlowSet},延迟={item.WaterDelayTime},开启={item.WaterOpenTime},交叉={item.WaterCrossTime}]";
        }
        private string DescribeCurrentChannels()=>_viceWindow==null?"不可用":string.Join(" | ",_viceWindow.ProcessCoolingChannels.Select(x=>DescribeCurrentChannel(x.Channel)));
    }
}

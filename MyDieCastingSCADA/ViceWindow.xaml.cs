using MahApps.Metro.IconPacks;
using MyDieCastingSCADA.UserControls;
using MySCADA.Models;
using MySCADA.Services;
using MySCADA.Business;
using MyDieCastingSCADA.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MyDieCastingSCADA
{
    public partial class ViceWindow : Window, INotifyPropertyChanged
    {
        private readonly PlcDevice _device;
        private readonly PlcService _plcService;
        private readonly CancellationTokenSource _pollingCts = new();
        private readonly SemaphoreSlim _pollCycleGate = new(1, 1);
        private Task? _pollingTask;
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);
        private DateTime _nextReconnectAttemptUtc = DateTime.MinValue;
        private int _consecutiveFailures;
        private bool _hasLoggedFirstSuccessfulSample;
        private bool _communicationFailureLogged;
        private readonly ScadaDatabaseService _databaseService;
        private readonly SqlServerDatabaseService _sqlServerService;
        private readonly SqlSyncService _sqlSyncService;
        private readonly RunningDataWriter _runningDataWriter;
        private readonly ProductionCycleService _productionCycleService;
        private readonly AlarmService _alarmService;
        private PlcRuntimeSnapshot? _latestSuccessfulSnapshot;
        private string? _lastSavedProcessJson;
        private bool _processChangeTrackingEnabled;
        private readonly DieCastingRunningBLL _runningBLL;
        private readonly DieCastingProcessBLL _processBLL;
        public DieCastingProcessBLL ProcessBLL => _processBLL;
        public DieCastingRunningBLL RunningBLL => _runningBLL;
        public string? LastOperationError => _processBLL.LastErrorMessage;
        public ObservableCollection<TemperatureItem> Temperatures { get; } = new();
        public ObservableCollection<CoolingChannelItem> CoolingChannels { get; } = new();
        public ObservableCollection<CoolingChannelItem> ProcessCoolingChannels { get; } = new();
        public ObservableCollection<int> CoolingChannelRows { get; } = new();
        public ObservableCollection<EditableChannelItem> EditableChannels { get; } = new();
        public ObservableCollection<TrendSample> TrendSamples { get; } = new();
        public ScadaDatabaseService DatabaseService => _databaseService;
        public SqlServerDatabaseService SqlServerService => _sqlServerService;
        public AlarmService AlarmService => _alarmService;
        public DieCastingProcess DownLoadDCP { get; set; } = new DieCastingProcess();
        public ObservableCollection<PressureItem> PressureItems { get; } = new();
        public ObservableCollection<TimeItem> TimeItems { get; } = new();
        public TemperatureItem ActualPressureItem { get; } = new() { Label = "压力" };
        public TemperatureItem AmountOfResidualAluminum { get; } = new() { Label = "剩余铝液" };
        public TemperatureItem ActualAluminiumTemperature { get; } = new() { Label = "铝液实际" };
        public TemperatureItem SetAluminiumTemperature { get; } = new() { Label = "铝液设定" };
        public TemperatureItem ActualFurnaceTemperature { get; } = new() { Label = "实际炉温" };
        public TemperatureItem SetFurnaceTemperature { get; } = new() { Label = "设置炉温" };
        public TemperatureItem CoolingTimeMaxItem { get; } = new() { Label = "冷却时间 Max" };
        public TemperatureItem CoolingTimeMinItem { get; } = new() { Label = "冷却时间 Min" };
        public TemperatureItem CastingOpenTempItem { get; } = new() { Label = "开模温度" };
        public TemperatureItem CastingOpenModeItem { get; } = new() { Label = "开模方式" };
        public TemperatureItem CastingOpenThermoItem { get; } = new() { Label = "开模热偶" };
        private string _communicationStatus = "状态：正在连接PLC...";
        public string CommunicationStatus
        {
            get => _communicationStatus;
            private set { if (_communicationStatus != value) { _communicationStatus = value; OnPropertyChanged(nameof(CommunicationStatus)); } }
        }
        private Brush _communicationStatusBrush = Brushes.DarkOrange;
        public Brush CommunicationStatusBrush
        {
            get => _communicationStatusBrush;
            private set { if (_communicationStatusBrush != value) { _communicationStatusBrush = value; OnPropertyChanged(nameof(CommunicationStatusBrush)); } }
        }
        private string _lastSuccessfulSampleText = "尚未成功采集";
        public string LastSuccessfulSampleText
        {
            get => _lastSuccessfulSampleText;
            private set { if (_lastSuccessfulSampleText != value) { _lastSuccessfulSampleText = value; OnPropertyChanged(nameof(LastSuccessfulSampleText)); } }
        }
        private string? _lastCommunicationError;
        public string? LastCommunicationError
        {
            get => _lastCommunicationError;
            private set { if (_lastCommunicationError != value) { _lastCommunicationError = value; OnPropertyChanged(nameof(LastCommunicationError)); } }
        }
        private string _databaseSyncStatus = "数据库同步：正在初始化";
        public string DatabaseSyncStatus
        {
            get => _databaseSyncStatus;
            private set
            {
                if (_databaseSyncStatus == value) return;
                _databaseSyncStatus = value;
                OnPropertyChanged(nameof(DatabaseSyncStatus));
            }
        }
        private int _editableCastingOpenMode = 0;
        public int EditableCastingOpenMode
        {
            get => _editableCastingOpenMode;
            set { if (_editableCastingOpenMode != value) { _editableCastingOpenMode = value; OnPropertyChanged(nameof(EditableCastingOpenMode)); } }
        }
        private float _editableCastingOpenTemp = 0f;
        public float EditableCastingOpenTemp
        {
            get => _editableCastingOpenTemp;
            set { if (_editableCastingOpenTemp != value) { _editableCastingOpenTemp = value; OnPropertyChanged(nameof(EditableCastingOpenTemp)); } }
        }
        private int _editableCastingOpenThermo = 0;
        public int EditableCastingOpenThermo
        {
            get => _editableCastingOpenThermo;
            set { if (_editableCastingOpenThermo != value) { _editableCastingOpenThermo = value; OnPropertyChanged(nameof(EditableCastingOpenThermo)); } }
        }
        private int _editableMaintainTime = 0;
        public int EditableMaintainTime
        {
            get => _editableMaintainTime;
            set { if (_editableMaintainTime != value) { _editableMaintainTime = value; OnPropertyChanged(nameof(EditableMaintainTime)); } }
        }
        private float _editableCoolingTimeMax = 0f;
        public float EditableCoolingTimeMax
        {
            get => _editableCoolingTimeMax;
            set { if (_editableCoolingTimeMax != value) { _editableCoolingTimeMax = value; OnPropertyChanged(nameof(EditableCoolingTimeMax)); } }
        }
        private float _editableCoolingTimeMin = 0f;
        public float EditableCoolingTimeMin
        {
            get => _editableCoolingTimeMin;
            set { if (_editableCoolingTimeMin != value) { _editableCoolingTimeMin = value; OnPropertyChanged(nameof(EditableCoolingTimeMin)); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ViceWindow(PlcDevice device)
        {
            InitializeComponent();
            _device = device;
            DataContext = this;
            this.Title = $"{device.DeviceName} - 实时监控";
            try
            {
                OperationLogService.Info("打开监控窗口", "开始初始化", $"设备：{device.DeviceName}，IP：{device.IpAddress}");
                _databaseService = DatabaseRuntime.Local;
                _sqlServerService = DatabaseRuntime.Remote;
                _sqlSyncService = DatabaseRuntime.Sync;
                DatabaseSyncStatus = _sqlSyncService.CurrentStatus;
                _sqlSyncService.StatusChanged += OnDatabaseStatusChanged;
                btnDatabaseWrite.Content = _sqlServerService.IsWritingEnabled ? "停止数据库写入" : "连接数据库";
                ApplyPermissions();
                _runningDataWriter = new RunningDataWriter(
                    device.DeviceName, _databaseService, _sqlServerService, _sqlSyncService);
                _productionCycleService = new ProductionCycleService(
                    device.DeviceName, _databaseService);
                _alarmService = new AlarmService(device.DeviceName, _databaseService);
                OperationLogService.Success("初始化数据库", "SQLite数据库已就绪", _databaseService.DatabasePath);
                var configService = new ConfigService(device.ConfigKey);
                var deviceConfig = configService.LoadDeviceConfig();
                var processConfig = configService.LoadProcessConfig();
                OperationLogService.Success("读取设备配置", "配置读取完成", $"配置标识：{device.ConfigKey}");
                _plcService = new PlcService(deviceConfig, processConfig);   
                bool connected = _plcService.Connect(device.IpAddress);
                _device.IsConnected = connected;
                if (connected)
                    OperationLogService.Success("连接 PLC", "连接成功", $"IP：{device.IpAddress}");
                else
                    OperationLogService.Warning("连接 PLC", "连接失败，将自动重试", $"IP：{device.IpAddress}");
                if (!connected)
                    MessageBox.Show($"PLC {device.IpAddress} 连接失败！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                _runningBLL = new DieCastingRunningBLL(_plcService, deviceConfig);
                var processWriteSafety = new ProcessWriteSafetyService();
                _processBLL = new DieCastingProcessBLL(
                    _plcService,
                    processConfig,
                    processWriteSafety,
                    () => _latestSuccessfulSnapshot);
                InitializeCollections(deviceConfig, processConfig);
                _pollingTask = PollingLoopAsync(_pollingCts.Token);
                LoadPage("Temperature");
                SetActiveNavigationButton(btnDeviceWindow);
            }
            catch (Exception ex)
            {
                _device.IsConnected = false;
                OperationLogService.Error("初始化监控窗口", "初始化失败", ex.Message);
                throw new InvalidOperationException($"窗口初始化失败：{ex.Message}", ex);
            }
        }
        private void InitializeCollections(PlcDeviceConfig deviceConfig, PlcProcessConfig processConfig)
        {
            Temperatures.Clear();
            for (int i = 0; i < deviceConfig.Temperatures.Length; i++)
            {
                Temperatures.Add(new TemperatureItem { Label = $"温度 {i + 1}" });
            }
            string[] stages = { "一段", "二段", "三段", "四段", "五段", "六段", "七段", "八段", "保压" };
            PressureItems.Clear();
            TimeItems.Clear();
            for (int i = 0; i < 9; i++)
            {
                PressureItems.Add(new PressureItem { Stage = stages[i] });
                TimeItems.Add(new TimeItem { Stage = stages[i] });
            }
            EditableChannels.Clear();
            for (int i = 0; i < processConfig.CoolingChannels.Length; i++)
            {
                EditableChannels.Add(new EditableChannelItem
                {
                    Channel = i + 1,
                    AirSetTemp = 0,
                    AirOpenTime = 0,
                    AirLastTime = 0,
                    AirFlow = 0,
                    AirConMode = 0,
                    AirTheNm = 0,
                    WaterSetTemp = 0,
                    WaterOpenTime = 0,
                    WaterLastTime = 0,
                    WaterFlow = 0,
                    WaterConMode = 0,
                    WaterTheNm = 0
                });
            }
            CoolingChannelRows.Clear();
            int rowCount = (processConfig.CoolingChannels.Length + 7) / 8;
            for (int row = 0; row < rowCount; row++)
            {
                CoolingChannelRows.Add(row + 1);
            }
        }
        private async Task PollingLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ViceWindow] 后台采集异常: {ex.Message}");
                    await Dispatcher.InvokeAsync(() => ApplySnapshot(new PlcRuntimeSnapshot
                    {
                        SampleTime = DateTime.Now,
                        IsConnected = _plcService.IsConnected,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    }));
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
        {
            await _pollCycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_plcService.IsConnected && DateTime.UtcNow >= _nextReconnectAttemptUtc)
                {
                    _nextReconnectAttemptUtc = DateTime.UtcNow + ReconnectInterval;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CommunicationStatus = "状态：正在重新连接PLC...";
                        CommunicationStatusBrush = Brushes.DarkOrange;
                        OperationLogService.Info("自动重连 PLC", "开始执行", $"IP：{_device.IpAddress}");
                    });
                    bool reconnected = await Task.Run(() => _plcService.TryReconnect(), cancellationToken)
                        .ConfigureAwait(false);
                    OperationLogService.Info("自动重连 PLC",
                        reconnected ? "连接成功" : "连接失败",
                        reconnected ? $"IP：{_device.IpAddress}" : "等待下一次自动重试");
                }
                PlcRuntimeSnapshot snapshot = await Task.Run(
                    () => _plcService.ReadRuntimeSnapshot(), cancellationToken).ConfigureAwait(false);
                _productionCycleService.ProcessSnapshot(snapshot);
                await Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
            }
            finally
            {
                _pollCycleGate.Release();
            }
        }
        private void ApplySnapshot(PlcRuntimeSnapshot snapshot)
        {
            _alarmService.Evaluate(snapshot);
            if (!snapshot.IsSuccess)
            {
                _consecutiveFailures++;
                LastCommunicationError = snapshot.ErrorMessage;
                if (!_communicationFailureLogged)
                {
                    OperationLogService.Warning("读取 PLC 实时数据", "采集失败，正在重试",
                        snapshot.ErrorMessage ?? "未返回详细错误信息");
                    _communicationFailureLogged = true;
                }
                if (_consecutiveFailures >= 3)
                {
                    _device.IsConnected = false;
                    CommunicationStatus = $"状态：PLC通信中断（连续失败 {_consecutiveFailures} 次）";
                    CommunicationStatusBrush = Brushes.Crimson;
                    if (_consecutiveFailures == 3)
                        OperationLogService.Error("PLC 通信监控", "通信已中断",
                            snapshot.ErrorMessage ?? "连续采集失败 3 次");
                }
                else
                {
                    CommunicationStatus = $"状态：采集失败，正在重试（{_consecutiveFailures}/3）";
                    CommunicationStatusBrush = Brushes.DarkOrange;
                }
                return;
            }
            _consecutiveFailures = 0;
            if (_communicationFailureLogged)
            {
                OperationLogService.Success("PLC 通信监控", "通信已恢复", $"恢复时间：{snapshot.SampleTime:HH:mm:ss}");
                _communicationFailureLogged = false;
                _hasLoggedFirstSuccessfulSample = true;
            }
            else if (!_hasLoggedFirstSuccessfulSample)
            {
                OperationLogService.Success("读取 PLC 实时数据", "首次采集完成", $"采集时间：{snapshot.SampleTime:HH:mm:ss}");
                _hasLoggedFirstSuccessfulSample = true;
            }
            _device.IsConnected = true;
            _latestSuccessfulSnapshot = snapshot;
            if (_processChangeTrackingEnabled)
                SaveRuntimeProcessSnapshotIfChanged(
                    snapshot,
                    _lastSavedProcessJson == null ? "工艺面板首次读取" : "PLC工艺参数变化");
            CommunicationStatus = "状态：PLC通信正常";
            CommunicationStatusBrush = Brushes.SeaGreen;
            LastSuccessfulSampleText = snapshot.SampleTime.ToString("yyyy-MM-dd HH:mm:ss");
            LastCommunicationError = null;
            for (int i = 0; i < snapshot.Temperatures.Length && i < Temperatures.Count; i++)
                Temperatures[i].Value = snapshot.Temperatures[i];
            ActualPressureItem.Value = snapshot.ActualPressure;
            AmountOfResidualAluminum.Value = snapshot.AmountOfResidualAluminum;
            ActualAluminiumTemperature.Value = snapshot.ActualAluminiumTemperature;
            SetAluminiumTemperature.Value = snapshot.SetAluminiumTemperature;
            ActualFurnaceTemperature.Value = snapshot.ActualFurnaceTemperature;
            SetFurnaceTemperature.Value = snapshot.SetFurnaceTemperature;
            while (CoolingChannels.Count < snapshot.CoolingChannels.Count)
                CoolingChannels.Add(new CoolingChannelItem());
            while (CoolingChannels.Count > snapshot.CoolingChannels.Count)
                CoolingChannels.RemoveAt(CoolingChannels.Count - 1);
            for (int i = 0; i < snapshot.CoolingChannels.Count; i++)
                CopyCoolingChannel(snapshot.CoolingChannels[i], CoolingChannels[i]);
            while (ProcessCoolingChannels.Count < snapshot.ProcessCoolingChannels.Count)
                ProcessCoolingChannels.Add(new CoolingChannelItem());
            while (ProcessCoolingChannels.Count > snapshot.ProcessCoolingChannels.Count)
                ProcessCoolingChannels.RemoveAt(ProcessCoolingChannels.Count - 1);
            for (int i = 0; i < snapshot.ProcessCoolingChannels.Count; i++)
                CopyCoolingChannel(snapshot.ProcessCoolingChannels[i], ProcessCoolingChannels[i]);
            for (int i = 0; i < snapshot.PressureValues.Length && i < PressureItems.Count; i++)
                PressureItems[i].Value = snapshot.PressureValues[i];
            for (int i = 0; i < snapshot.TimeValues.Length && i < TimeItems.Count; i++)
                TimeItems[i].Value = snapshot.TimeValues[i];
            CoolingTimeMaxItem.Value = snapshot.CoolingTimeMax;
            CoolingTimeMinItem.Value = snapshot.CoolingTimeMin;
            CastingOpenTempItem.Value = snapshot.CastingOpenTemperature;
            CastingOpenModeItem.Value = snapshot.CastingOpenMode;
            CastingOpenThermoItem.Value = snapshot.CastingOpenThermocoupleNm;
            TrendSamples.Add(new TrendSample
            {
                Time = snapshot.SampleTime,
                Temperatures = snapshot.Temperatures.ToArray(),
                AluminiumTemperature = snapshot.ActualAluminiumTemperature,
                Pressure = snapshot.ActualPressure
            });
            while (TrendSamples.Count > 180) TrendSamples.RemoveAt(0);
            _runningDataWriter.TryEnqueue(snapshot);
        }
        private static void CopyCoolingChannel(CoolingChannelItem source, CoolingChannelItem target)
        {
            target.Channel = source.Channel;
            target.AirFlowSet = source.AirFlowSet;
            target.WaterFlowSet = source.WaterFlowSet;
            target.AirActual = source.AirActual;
            target.WaterActual = source.WaterActual;
            target.AirAuMode = source.AirAuMode;
            target.AirTheNm = source.AirTheNm;
            target.AirTemp = source.AirTemp;
            target.AirConMode = source.AirConMode;
            target.AirDelayTime = source.AirDelayTime;
            target.AirOpenTime = source.AirOpenTime;
            target.AirCrossTime = source.AirCrossTime;
            target.WaterAuMode = source.WaterAuMode;
            target.WaterTheNm = source.WaterTheNm;
            target.WaterTemp = source.WaterTemp;
            target.WaterConMode = source.WaterConMode;
            target.WaterDelayTime = source.WaterDelayTime;
            target.WaterOpenTime = source.WaterOpenTime;
            target.WaterCrossTime = source.WaterCrossTime;
        }
        public bool DownloadSingleChannel(int channelIndex, EditableChannelItem? editItem)
        {
            if (editItem == null) return false;
            return _processBLL.DownloadSingleChannel(channelIndex, editItem);
        }
        public bool DownloadAllChannels()
        {
            return _processBLL.DownloadAllChannels(EditableChannels);
        }
        public bool DownloadOpenParam()
        {
            return _processBLL.DownloadOpenParam(
                EditableCastingOpenMode,
                EditableCastingOpenTemp,
                EditableCastingOpenThermo);
        }
        public bool DownloadMaintainCoolTime()
        {
            return _processBLL.DownloadMaintainCoolTime(
                EditableMaintainTime,
                EditableCoolingTimeMax,
                EditableCoolingTimeMin);
        }
        protected override void OnClosed(EventArgs e)
        {
            OperationLogService.Info("关闭监控窗口", "正在停止后台采集", $"设备：{_device.DeviceName}");
            _pollingCts.Cancel();
            _sqlSyncService.StatusChanged -= OnDatabaseStatusChanged;
            base.OnClosed(e);
            _ = DisposeAfterPollingAsync();
        }
        public void SaveProcessSnapshot(string source)
        {
            _databaseService.SaveProcessSnapshotWithSync(_device.DeviceName,source,DownLoadDCP);
            _sqlSyncService.NotifyNewData();
        }
        private void OpenHistoryPage()
        {
            MainContent.Content = new UserControls.HistoryPage();
            _processChangeTrackingEnabled = true;
            if (_latestSuccessfulSnapshot != null && _lastSavedProcessJson == null)
                SaveRuntimeProcessSnapshotIfChanged(
                    _latestSuccessfulSnapshot, "打开工艺面板");
        }
        private void SaveRuntimeProcessSnapshotIfChanged(
            PlcRuntimeSnapshot snapshot, string source, bool forceSave = false)
        {
            string processJson = JsonSerializer.Serialize(new
            {
                snapshot.PressureValues,
                snapshot.TimeValues,
                snapshot.CoolingTimeMax,
                snapshot.CoolingTimeMin,
                snapshot.CastingOpenTemperature,
                snapshot.CastingOpenMode,
                snapshot.CastingOpenThermocoupleNm,
                CoolingChannels = snapshot.ProcessCoolingChannels
            });
            if (!forceSave && string.Equals(
                    processJson, _lastSavedProcessJson, StringComparison.Ordinal))
                return;
            _lastSavedProcessJson = processJson;
            _ = Task.Run(() =>
            {
                try
                {
                    _databaseService.SaveProcessHistorySummaryWithSync(_device.DeviceName,source,snapshot);
                    _sqlSyncService.NotifyNewData();
                    OperationLogService.Success("工艺参数历史", "快照已保存", source);
                }
                catch (Exception ex)
                {
                    OperationLogService.Warning("工艺参数历史", "快照保存失败", ex.Message);
                }
            });
        }
        private async Task DisposeAfterPollingAsync()
        {
            try
            {
                if (_pollingTask != null)
                    await _pollingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await _runningDataWriter.DisposeAsync().ConfigureAwait(false);
                _plcService.Dispose();
                _pollCycleGate.Dispose();
                _pollingCts.Dispose();
            }
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void MaxRestoreButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();
        private async void DatabaseWriteButton_Click(object sender, RoutedEventArgs e)
        {
            if(!AuthorizationService.Require(AppPermission.ManageDatabase,out string permissionError))
            { MessageBox.Show(permissionError,"权限不足",MessageBoxButton.OK,MessageBoxImage.Warning);return; }
            if (_sqlServerService.IsWritingEnabled)
            {
                _sqlSyncService.DisableWriting();
                OperationLogService.Info("SQL Server", "远程写入已停止", "PLC数据仍继续保存到本地SQLite");
                return;
            }
            btnDatabaseWrite.IsEnabled = false;
            btnDatabaseWrite.Content = "正在连接...";
            try
            {
                var result = await _sqlSyncService.EnableWritingAsync();
                if (result.Success)
                {
                    btnDatabaseWrite.Content = "停止数据库写入";
                    _sqlSyncService.RefreshStatus();
                    OperationLogService.Success("SQL Server", "连接成功，远程写入已开启", result.Message);
                }
                else
                {
                    btnDatabaseWrite.Content = "连接数据库";
                    _sqlSyncService.RefreshStatus();
                    OperationLogService.Warning("SQL Server", "连接失败，仍仅写入SQLite", result.Message);
                    MessageBox.Show(result.Message, "数据库连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                btnDatabaseWrite.IsEnabled = true;
            }
        }
        private void OnDatabaseStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DatabaseSyncStatus=status;
                btnDatabaseWrite.Content=_sqlServerService.IsWritingEnabled?"停止数据库写入":"连接数据库";
            }));
        }
        private void ApplyPermissions()
        {
            btnParameter.IsEnabled=AuthorizationService.Has(AppPermission.ManageProcess);
            btnMold.Visibility=AuthorizationService.Has(AppPermission.ManageMolds)?Visibility.Visible:Visibility.Collapsed;
            btnSync.Visibility=AuthorizationService.Has(AppPermission.ManageDatabase)?Visibility.Visible:Visibility.Collapsed;
            btnDatabaseWrite.IsEnabled=AuthorizationService.Has(AppPermission.ManageDatabase);
            btnUserManagement.Visibility=AuthorizationService.Has(AppPermission.ManageUsers)?Visibility.Visible:Visibility.Collapsed;
        }
        private void UserManagement_Click(object sender,RoutedEventArgs e)
        {
            if(!AuthorizationService.Require(AppPermission.ManageUsers,out string error)){MessageBox.Show(error);return;}
            new UserManagementWindow{Owner=this}.ShowDialog();
        }
        private void Logout_Click(object sender,RoutedEventArgs e)
        {
            if(MessageBox.Show("确定退出当前账号并返回登录窗口吗？","退出登录",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            OperationLogService.Info("用户退出","退出登录",AuthorizationService.CurrentUser?.Username??"");
            App.Relogin();
        }
        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string pageName = btn.Content?.ToString() ?? btn.Tag.ToString()!;
                OperationLogService.Info("切换页面", "页面已打开", pageName);
                LoadPage(btn.Tag.ToString());
                SetActiveNavigationButton(btn);
            }
        }
        private void SetActiveNavigationButton(Button selectedButton)
        {
            foreach (Button button in TopNavigationPanel.Children.OfType<Button>())
            {
                bool isSelected = ReferenceEquals(button, selectedButton);
                button.Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(83, 68, 166))
                    : Brushes.Transparent;
                button.BorderBrush = isSelected ? Brushes.White : Brushes.Transparent;
                button.BorderThickness = isSelected ? new Thickness(0, 0, 0, 3) : new Thickness(0);
                button.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
                button.Opacity = isSelected ? 1.0 : 0.78;
            }
            string pageName = selectedButton.Content?.ToString() ?? "未知页面";
            txtCurrentPage.Text = $"{pageName} · {AuthorizationService.CurrentUser?.Username}（{AuthorizationService.CurrentUser?.RoleText}）";
            Title = $"{_device.DeviceName} - {pageName}";
        }
        private void LoadPage(string pageKey)
        {
            switch (pageKey)
            {
                case "Temperature": MainContent.Content = new UserControls.TemperaturePage(); break;
                case "RealTimeLog": MainContent.Content = new UserControls.RealTimeLogPage(); break;
                case "Parameter": MainContent.Content = new UserControls.ParameterPage(); break;
                case "Alarm": MainContent.Content = new UserControls.AlarmPage(_alarmService); break;
                case "History": OpenHistoryPage(); break;
                case "Trend": MainContent.Content = new UserControls.TrendPage(this); break;
                case "Data": MainContent.Content = new UserControls.DataHistoryPage(_databaseService, _sqlServerService); break;
                case "Sync": MainContent.Content = new UserControls.SyncStatusPage(); break;
                case "Mold": MainContent.Content = new UserControls.MoldManagementPage(_databaseService, _sqlServerService); break;
                case "Control": MainContent.Content = new UserControls.ControlResearchPage(); break;
                default: MainContent.Content = new TextBlock { Text = "页面开发中..." }; break;
            }
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

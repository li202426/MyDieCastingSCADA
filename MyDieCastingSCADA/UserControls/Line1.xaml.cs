using MyDieCastingSCADA.UserControls;
using MySCADA.Models;
using MySCADA.Services;
using S7.Net;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace MyDieCastingSCADA.UserControls
{
    public partial class Line1 : UserControl
    {
        private readonly DispatcherTimer _refreshTimer;
        private bool _isScanning;
        public ObservableCollection<PlcDevice> PlcDevices { get; } = new ObservableCollection<PlcDevice>();
        public ObservableCollection<TemperatureItem> Temperatures { get; } = new ObservableCollection<TemperatureItem>();
        public Line1()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += Line1_Loaded;
            Unloaded += Line1_Unloaded;
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _refreshTimer.Tick += async (s, e) => await BackgroundScanAsync();
        }
        private async void Line1_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPlcConfig();
            _refreshTimer.Start();
            await BackgroundScanAsync();   
        }
        private void Line1_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }
        private async Task BackgroundScanAsync()
        {
            if (_isScanning) return;
            _isScanning = true;
            try
            {
                await Task.Run(async () =>
                {
                    foreach (var device in PlcDevices.ToList())
                    {
                        bool reachable = await IsDeviceReachableAsync(device.IpAddress);
                        await Dispatcher.InvokeAsync(() => device.IsConnected = reachable);
                    }
                });
            }
            finally
            {
                _isScanning = false;
            }
        }
        private async Task<bool> IsDeviceReachableAsync(string ip)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 800);
                if (reply.Status != IPStatus.Success)
                    return false;
                return await Task.Run(() =>
                {
                    try
                    {
                        using var testPlc = new Plc(CpuType.S71500, ip, 0, 1);
                        testPlc.Open();
                        bool connected = testPlc.IsConnected;
                        testPlc.Close();
                        return connected;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch
            {
                return false;
            }
        }
        private void LoadPlcConfig()
        {
            try
            {
                string configFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
                string jsonPath = Path.Combine(configFolder, "Line1Config.json");
                if (!Directory.Exists(configFolder))
                    Directory.CreateDirectory(configFolder);
                if (!File.Exists(jsonPath))
                {
                    MessageBox.Show($"配置文件不存在！\n路径：{jsonPath}", "警告",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string json = File.ReadAllText(jsonPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict == null || dict.Count == 0) return;
                PlcDevices.Clear();
                foreach (var item in dict)
                {
                    var device = new PlcDevice
                    {
                        DeviceName = $"压铸机 {item.Key} 号机",
                        ConfigKey = $"L1-{item.Key}",
                        IpAddress = item.Value,
                        DisplayName = $"压铸机 {item.Key} 号机",
                        Ip = item.Value,
                        Tag = item.Key,
                        MachineNum = item.Key.TrimStart('A', 'B', 'C')
                    };
                    PlcDevices.Add(device);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置文件失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PlcButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var device = PlcDevices.FirstOrDefault(d => d.Tag == tag);
                if (device == null)
                {
                    MessageBox.Show("未找到该设备", "提示");
                    return;
                }
                try
                {
                    var window = new ViceWindow(device);
                    window.Owner = Window.GetWindow(this);
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.Title = $"{device.DeviceName} - 实时监控";
                    window.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开监控窗口失败：\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

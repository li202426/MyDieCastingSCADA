using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MySCADA.Models;
using MySCADA.Services;         
using MyDieCastingSCADA.UserControls;   
namespace MyDieCastingSCADA.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<PlcDevice> PlcDevices { get; } = new();
        public MainViewModel()
        {
            PlcDevices.Add(new PlcDevice
            {
                DeviceName = "L1-A1",
                IpAddress = "192.168.200.32",
                ProductionLine = "L1"
            });
            PlcDevices.Add(new PlcDevice
            {
                DeviceName = "L1-A2",
                IpAddress = "192.168.200.33",
                ProductionLine = "L1"
            });
        }
        [RelayCommand]
        private void OpenDevice(PlcDevice device)
        {
            if (device == null) return;
            var window = new ViceWindow(device);
            window.Show();
        }
    }
}
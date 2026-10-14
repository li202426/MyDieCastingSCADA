using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace MySCADA.Models
{
    public class PlcDevice : INotifyPropertyChanged
    {
        public string DeviceName { get; set; } = "";
        public string ConfigKey { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string ProductionLine { get; set; } = "";
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
        public string StatusText => IsConnected ? "在线" : "离线";
        public string DisplayName { get; set; } = "";
        public string Ip { get => IpAddress; set => IpAddress = value; }
        public string Tag { get; set; } = "";
        public string MachineNum { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

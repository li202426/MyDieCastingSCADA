using System.ComponentModel;
namespace MySCADA.Models
{
    public class CoolingChannelItem : INotifyPropertyChanged
    {
        private int _channel;
        public int Channel
        {
            get => _channel;
            set { if (_channel != value) { _channel = value; OnPropertyChanged(nameof(Channel)); } }
        }
        private int _airFlowSet;
        public int AirFlowSet { get => _airFlowSet; set { if (_airFlowSet != value) { _airFlowSet = value; OnPropertyChanged(nameof(AirFlowSet)); } } }
        private int _waterFlowSet;
        public int WaterFlowSet { get => _waterFlowSet; set { if (_waterFlowSet != value) { _waterFlowSet = value; OnPropertyChanged(nameof(WaterFlowSet)); } } }
        private int? _airActual;
        public int? AirActual { get => _airActual; set { if (_airActual != value) { _airActual = value; OnPropertyChanged(nameof(AirActual)); } } }
        private int? _waterActual;
        public int? WaterActual { get => _waterActual; set { if (_waterActual != value) { _waterActual = value; OnPropertyChanged(nameof(WaterActual)); } } }
        private int _airAuMode;
        public int AirAuMode { get => _airAuMode; set { if (_airAuMode != value) { _airAuMode = value; OnPropertyChanged(nameof(AirAuMode)); } } }
        private int _airTheNm;
        public int AirTheNm { get => _airTheNm; set { if (_airTheNm != value) { _airTheNm = value; OnPropertyChanged(nameof(AirTheNm)); } } }
        private float _airTemp;
        public float AirTemp { get => _airTemp; set { if (_airTemp != value) { _airTemp = value; OnPropertyChanged(nameof(AirTemp)); } } }
        private int _airConMode;
        public int AirConMode { get => _airConMode; set { if (_airConMode != value) { _airConMode = value; OnPropertyChanged(nameof(AirConMode)); } } }
        private int _airDelayTime;
        public int AirDelayTime { get => _airDelayTime; set { if (_airDelayTime != value) { _airDelayTime = value; OnPropertyChanged(nameof(AirDelayTime)); } } }
        private int _airOpenTime;
        public int AirOpenTime { get => _airOpenTime; set { if (_airOpenTime != value) { _airOpenTime = value; OnPropertyChanged(nameof(AirOpenTime)); } } }
        private int _airCrossTime;
        public int AirCrossTime { get => _airCrossTime; set { if (_airCrossTime != value) { _airCrossTime = value; OnPropertyChanged(nameof(AirCrossTime)); } } }
        private int _waterAuMode;
        public int WaterAuMode { get => _waterAuMode; set { if (_waterAuMode != value) { _waterAuMode = value; OnPropertyChanged(nameof(WaterAuMode)); } } }
        private int _waterTheNm;
        public int WaterTheNm { get => _waterTheNm; set { if (_waterTheNm != value) { _waterTheNm = value; OnPropertyChanged(nameof(WaterTheNm)); } } }
        private float _waterTemp;
        public float WaterTemp { get => _waterTemp; set { if (_waterTemp != value) { _waterTemp = value; OnPropertyChanged(nameof(WaterTemp)); } } }
        private int _waterConMode;
        public int WaterConMode { get => _waterConMode; set { if (_waterConMode != value) { _waterConMode = value; OnPropertyChanged(nameof(WaterConMode)); } } }
        private int _waterDelayTime;
        public int WaterDelayTime { get => _waterDelayTime; set { if (_waterDelayTime != value) { _waterDelayTime = value; OnPropertyChanged(nameof(WaterDelayTime)); } } }
        private int _waterOpenTime;
        public int WaterOpenTime { get => _waterOpenTime; set { if (_waterOpenTime != value) { _waterOpenTime = value; OnPropertyChanged(nameof(WaterOpenTime)); } } }
        private int _waterCrossTime;
        public int WaterCrossTime { get => _waterCrossTime; set { if (_waterCrossTime != value) { _waterCrossTime = value; OnPropertyChanged(nameof(WaterCrossTime)); } } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string AirControlModeText => AirConMode == 1 ? "温度" : "时间";
        public string WaterControlModeText => WaterConMode == 1 ? "温度" : "时间";
        public string AirStatusColor => AirAuMode > 0 ? "#27AE60" : "#E74C3C";
    }
}

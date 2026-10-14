using System.ComponentModel;
namespace MySCADA.Models
{
    public class EditableChannelItem : INotifyPropertyChanged
    {
        public int Channel { get; set; }
        private float _airSetTemp;
        public float AirSetTemp
        {
            get => _airSetTemp;
            set { _airSetTemp = value; OnPropertyChanged(nameof(AirSetTemp)); }
        }
        private int _airOpenTime;
        public int AirOpenTime
        {
            get => _airOpenTime;
            set { _airOpenTime = value; OnPropertyChanged(nameof(AirOpenTime)); }
        }
        private int _airLastTime;
        public int AirLastTime
        {
            get => _airLastTime;
            set { _airLastTime = value; OnPropertyChanged(nameof(AirLastTime)); }
        }
        private int _airFlow;
        public int AirFlow
        {
            get => _airFlow;
            set { _airFlow = value; OnPropertyChanged(nameof(AirFlow)); }
        }
        private int _airConMode;
        public int AirConMode
        {
            get => _airConMode;
            set { _airConMode = value; OnPropertyChanged(nameof(AirConMode)); }
        }
        private int _airTheNm;
        public int AirTheNm
        {
            get => _airTheNm;
            set { _airTheNm = value; OnPropertyChanged(nameof(AirTheNm)); }
        }
        private int _airCrossTime;
        public int AirCrossTime
        {
            get => _airCrossTime;
            set { _airCrossTime = value; OnPropertyChanged(nameof(AirCrossTime)); }
        }
        private float _waterSetTemp;
        public float WaterSetTemp
        {
            get => _waterSetTemp;
            set { _waterSetTemp = value; OnPropertyChanged(nameof(WaterSetTemp)); }
        }
        private int _waterOpenTime;
        public int WaterOpenTime
        {
            get => _waterOpenTime;
            set { _waterOpenTime = value; OnPropertyChanged(nameof(WaterOpenTime)); }
        }
        private int _waterLastTime;
        public int WaterLastTime
        {
            get => _waterLastTime;
            set { _waterLastTime = value; OnPropertyChanged(nameof(WaterLastTime)); }
        }
        private int _waterFlow;
        public int WaterFlow
        {
            get => _waterFlow;
            set { _waterFlow = value; OnPropertyChanged(nameof(WaterFlow)); }
        }
        private int _waterConMode;
        public int WaterConMode
        {
            get => _waterConMode;
            set { _waterConMode = value; OnPropertyChanged(nameof(WaterConMode)); }
        }
        private int _waterTheNm;
        public int WaterTheNm
        {
            get => _waterTheNm;
            set { _waterTheNm = value; OnPropertyChanged(nameof(WaterTheNm)); }
        }
        private int _waterCrossTime;
        public int WaterCrossTime
        {
            get => _waterCrossTime;
            set { _waterCrossTime = value; OnPropertyChanged(nameof(WaterCrossTime)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
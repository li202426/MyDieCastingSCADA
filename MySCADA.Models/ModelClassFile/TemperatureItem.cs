using System.ComponentModel;
namespace MySCADA.Models
{
    public class TemperatureItem : INotifyPropertyChanged
    {
        private string _label = string.Empty;
        private float _value = -1f;
        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; OnPropertyChanged(nameof(Label)); } }
        }
        public float Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(nameof(Value)); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
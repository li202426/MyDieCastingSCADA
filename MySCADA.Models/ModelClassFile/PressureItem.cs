using System.ComponentModel;
namespace MySCADA.Models
{
    public class PressureItem : INotifyPropertyChanged
    {
        public string Stage { get; set; } = "";
        private float _value;
        public float Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
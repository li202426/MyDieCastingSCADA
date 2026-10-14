using System.ComponentModel;
namespace MySCADA.Models
{
    public class TimeItem : INotifyPropertyChanged
    {
        public string Stage { get; set; } = "";
        private int _value;
        public int Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
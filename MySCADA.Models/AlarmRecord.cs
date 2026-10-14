using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace MySCADA.Models;
public class AlarmRecord : INotifyPropertyChanged
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    private DateTime? _recoveredAt;
    public DateTime? RecoveredAt { get => _recoveredAt; set { _recoveredAt=value; Notify(); Notify(nameof(RecoveredAtText)); } }
    public string DeviceName { get; set; } = "";
    public string Code { get; set; } = "";
    public string Level { get; set; } = "警告";
    public string Message { get; set; } = "";
    private string _currentValue = "";
    public string CurrentValue { get => _currentValue; set { _currentValue=value; Notify(); } }
    private bool _isActive = true;
    private bool _isAcknowledged;
    public bool IsActive { get => _isActive; set { _isActive = value; Notify(); Notify(nameof(StatusText)); } }
    public bool IsAcknowledged { get => _isAcknowledged; set { _isAcknowledged = value; Notify(); Notify(nameof(StatusText)); } }
    public string StatusText => IsActive ? (IsAcknowledged ? "活动-已确认" : "活动-未确认") : "已恢复";
    public string OccurredAtText => OccurredAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string RecoveredAtText => RecoveredAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

using MySCADA.Models;
using MySCADA.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
namespace MyDieCastingSCADA.Services;
public sealed class AlarmService
{
    private readonly string _deviceName;
    private readonly ScadaDatabaseService _database;
    private readonly Dictionary<string, AlarmRecord> _active = new();
    private readonly Dictionary<string, PendingTransition> _pending = new();
    private readonly AlarmSettings _settings;
    public ObservableCollection<AlarmRecord> Records { get; } = new();
    public AlarmService(string deviceName, ScadaDatabaseService database)
    {
        _deviceName=deviceName; _database=database;
        _settings=LoadSettings();
        foreach(var alarm in database.GetRecentAlarms())
        {
            Records.Add(alarm);
            if(alarm.IsActive && alarm.DeviceName==deviceName) _active[alarm.Code]=alarm;
        }
    }
    public void Evaluate(PlcRuntimeSnapshot s)
    {
        Set("PLC_COMM", _settings.CommunicationAlarmEnabled && !s.IsSuccess, "严重", "PLC通信中断", s.ErrorMessage ?? "读取失败");
        if (!s.IsSuccess) return;
        bool aluminiumHigh = _settings.TemperatureAlarmEnabled && s.SetAluminiumTemperature > 0 &&
            s.ActualAluminiumTemperature > s.SetAluminiumTemperature + _settings.AluminiumHighDeviation -
            (IsActive("AL_TEMP_HIGH") ? _settings.TemperatureRecoveryHysteresis : 0);
        Set("AL_TEMP_HIGH", aluminiumHigh,
            "警告", $"铝液实际温度超过设定值{_settings.AluminiumHighDeviation:F0}℃", $"实际 {s.ActualAluminiumTemperature:F1}℃ / 设定 {s.SetAluminiumTemperature:F1}℃");
        bool furnaceHigh = _settings.TemperatureAlarmEnabled && s.SetFurnaceTemperature > 0 &&
            s.ActualFurnaceTemperature > s.SetFurnaceTemperature + _settings.FurnaceHighDeviation -
            (IsActive("FURNACE_TEMP_HIGH") ? _settings.TemperatureRecoveryHysteresis : 0);
        Set("FURNACE_TEMP_HIGH", furnaceHigh,
            "警告", $"炉温实际值超过设定值{_settings.FurnaceHighDeviation:F0}℃", $"实际 {s.ActualFurnaceTemperature:F1}℃ / 设定 {s.SetFurnaceTemperature:F1}℃");
        foreach (var ch in s.CoolingChannels)
        {
            string airCode = $"AIR_FLOW_LOW_{ch.Channel}";
            string waterCode = $"WATER_FLOW_LOW_{ch.Channel}";
            double airRatio = IsActive(airCode) ? _settings.FlowRecoveryRatio : _settings.MinimumFlowRatio;
            double waterRatio = IsActive(waterCode) ? _settings.FlowRecoveryRatio : _settings.MinimumFlowRatio;
            Set(airCode, _settings.FlowAlarmEnabled && ch.AirFlowSet > 0 && ch.AirActual.HasValue && ch.AirActual.Value < ch.AirFlowSet * airRatio,
                "警告", $"冷却通道{ch.Channel}风流量偏低", $"实际 {ch.AirActual} / 设定 {ch.AirFlowSet}");
            Set(waterCode, _settings.FlowAlarmEnabled && ch.WaterFlowSet > 0 && ch.WaterActual.HasValue && ch.WaterActual.Value < ch.WaterFlowSet * waterRatio,
                "警告", $"冷却通道{ch.Channel}水流量偏低", $"实际 {ch.WaterActual} / 设定 {ch.WaterFlowSet}");
        }
    }
    private static AlarmSettings LoadSettings()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"Configs","AlarmSettings.json");
        try
        {
            if(!File.Exists(path)) return new AlarmSettings();
            AlarmSettings settings = JsonSerializer.Deserialize<AlarmSettings>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AlarmSettings();
            settings.MinimumFlowRatio = Math.Clamp(settings.MinimumFlowRatio, 0, 1);
            settings.FlowRecoveryRatio = Math.Clamp(settings.FlowRecoveryRatio, settings.MinimumFlowRatio, 1);
            settings.TemperatureRecoveryHysteresis = Math.Max(0, settings.TemperatureRecoveryHysteresis);
            settings.TriggerDelaySeconds = Math.Clamp(settings.TriggerDelaySeconds, 0, 300);
            settings.RecoveryDelaySeconds = Math.Clamp(settings.RecoveryDelaySeconds, 0, 300);
            return settings;
        }
        catch(Exception ex)
        {
            OperationLogService.Warning("读取报警配置","使用默认报警阈值",ex.Message);
            return new AlarmSettings();
        }
    }
    public void Acknowledge(AlarmRecord? record)
    {
        if (record == null || record.IsAcknowledged) return;
        if(!AuthorizationService.Require(AppPermission.AcknowledgeAlarm,out string error))
        { OperationLogService.Warning("报警确认","权限不足",error);return; }
        record.IsAcknowledged=true;
        _database.UpdateAlarmWithSync(record);
        OperationLogService.Info("报警确认", "确认完成", record.Message);
    }
    private void Set(string code, bool condition, string level, string message, string value)
    {
        bool isActive = IsActive(code);
        if (condition == isActive)
        {
            _pending.Remove(code);
            if (isActive && _active.TryGetValue(code, out AlarmRecord? current))
                current.CurrentValue = value;
            return;
        }
        DateTime now = DateTime.Now;
        if (!_pending.TryGetValue(code, out PendingTransition? transition) ||
            transition.TargetActive != condition)
        {
            _pending[code] = new PendingTransition(condition, now);
            return;
        }
        int delaySeconds = condition
            ? Math.Max(0, _settings.TriggerDelaySeconds)
            : Math.Max(0, _settings.RecoveryDelaySeconds);
        if ((now - transition.StartedAt).TotalSeconds < delaySeconds) return;
        _pending.Remove(code);
        if (condition)
        {
            var alarm=new AlarmRecord { DeviceName=_deviceName,Code=code,Level=level,Message=message,CurrentValue=value };
            alarm.Id=_database.InsertAlarmWithSync(alarm); _active[code]=alarm; Records.Insert(0,alarm);
            OperationLogService.Error("设备报警", "报警发生", $"{message}；{value}");
        }
        else if (_active.Remove(code, out var recovered))
        {
            recovered.IsActive=false; recovered.RecoveredAt=DateTime.Now; recovered.CurrentValue=value;
            _database.UpdateAlarmWithSync(recovered);
            OperationLogService.Success("设备报警", "报警恢复", recovered.Message);
        }
    }
    private bool IsActive(string code) => _active.ContainsKey(code);
    private sealed record PendingTransition(bool TargetActive, DateTime StartedAt);
}

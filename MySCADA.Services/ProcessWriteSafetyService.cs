using MySCADA.Models;
using System.Text.Json;
namespace MySCADA.Services;
public sealed class ProcessWriteSafetyService
{
    private readonly ProcessWriteSafetySettings _settings;
    public ProcessWriteSafetyService(string? configPath = null)
    {
        configPath ??= Path.Combine(
            AppContext.BaseDirectory, "Configs", "ProcessWriteSafetySettings.json");
        _settings = File.Exists(configPath)
            ? JsonSerializer.Deserialize<ProcessWriteSafetySettings>(
                File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new();
        NormalizeSettings();
    }
    public bool ValidateWriteState(
        bool plcConnected, PlcRuntimeSnapshot? latestSnapshot, out string error)
    {
        if (!_settings.WritesEnabled)
            return Fail("工艺写入总开关已关闭，请由管理员确认配置。", out error);
        if (!plcConnected)
            return Fail("PLC未连接，禁止下发工艺参数。", out error);
        if (latestSnapshot == null || !latestSnapshot.IsSuccess)
            return Fail("尚未获得有效PLC快照，禁止下发工艺参数。", out error);
        if ((DateTime.Now - latestSnapshot.SampleTime).TotalSeconds > _settings.MaxSnapshotAgeSeconds)
            return Fail($"PLC数据已超过{_settings.MaxSnapshotAgeSeconds}秒未更新，禁止下发工艺参数。", out error);
        if (!_settings.AllowWriteWhileInjectionRunning && latestSnapshot.InjuctionRun)
            return Fail("设备正在压射运行，当前安全配置禁止修改工艺参数。", out error);
        error = string.Empty;
        return true;
    }
    public bool ValidateChannel(EditableChannelItem item, out string error)
    {
        if (item == null) return Fail("冷却通道参数不能为空。", out error);
        var errors = new List<string>();
        Range(item.AirConMode, _settings.MinimumControlMode, _settings.MaximumControlMode, "风冷控制模式", errors);
        Range(item.WaterConMode, _settings.MinimumControlMode, _settings.MaximumControlMode, "水冷控制模式", errors);
        Range(item.AirTheNm, _settings.MinimumThermocoupleNumber, _settings.MaximumThermocoupleNumber, "风冷热电偶编号", errors);
        Range(item.WaterTheNm, _settings.MinimumThermocoupleNumber, _settings.MaximumThermocoupleNumber, "水冷热电偶编号", errors);
        Range(item.AirSetTemp, _settings.MinimumTemperature, _settings.MaximumTemperature, "风冷设定温度", errors);
        Range(item.WaterSetTemp, _settings.MinimumTemperature, _settings.MaximumTemperature, "水冷设定温度", errors);
        Range(item.AirFlow, _settings.MinimumFlow, _settings.MaximumFlow, "风冷流量", errors);
        Range(item.WaterFlow, _settings.MinimumFlow, _settings.MaximumFlow, "水冷流量", errors);
        Range(item.AirLastTime, _settings.MinimumTime, _settings.MaximumTime, "风冷延迟时间", errors);
        Range(item.WaterLastTime, _settings.MinimumTime, _settings.MaximumTime, "水冷延迟时间", errors);
        Range(item.AirOpenTime, _settings.MinimumTime, _settings.MaximumTime, "风冷开启时长", errors);
        Range(item.WaterOpenTime, _settings.MinimumTime, _settings.MaximumTime, "水冷开启时长", errors);
        Range(item.AirCrossTime, _settings.MinimumTime, _settings.MaximumTime, "风冷交叉时间", errors);
        Range(item.WaterCrossTime, _settings.MinimumTime, _settings.MaximumTime, "水冷交叉时间", errors);
        error = errors.Count == 0
            ? string.Empty
            : $"通道 {item.Channel} 参数不合法：{string.Join("；", errors)}";
        return errors.Count == 0;
    }
    public bool ValidateOpenParameters(int mode, float temperature, int thermocouple, out string error)
    {
        var errors = new List<string>();
        Range(mode, _settings.MinimumControlMode, _settings.MaximumControlMode, "开模方式", errors);
        Range(temperature, _settings.MinimumTemperature, _settings.MaximumTemperature, "开模温度", errors);
        Range(thermocouple, _settings.MinimumThermocoupleNumber, _settings.MaximumThermocoupleNumber, "开模热电偶编号", errors);
        error = string.Join("；", errors);
        return errors.Count == 0;
    }
    public bool ValidateMaintainParameters(
        int maintainTime, float coolingMaximum, float coolingMinimum, out string error)
    {
        var errors = new List<string>();
        Range(maintainTime, _settings.MinimumTime, _settings.MaximumTime, "保压时间", errors);
        Range(coolingMaximum, _settings.MinimumTime, _settings.MaximumTime, "冷却时间Max", errors);
        Range(coolingMinimum, _settings.MinimumTime, _settings.MaximumTime, "冷却时间Min", errors);
        if (coolingMinimum > coolingMaximum)
            errors.Add("冷却时间Min不能大于冷却时间Max");
        error = string.Join("；", errors);
        return errors.Count == 0;
    }
    private void NormalizeSettings()
    {
        _settings.MaxSnapshotAgeSeconds = Math.Max(1, _settings.MaxSnapshotAgeSeconds);
        if (_settings.MaximumControlMode < _settings.MinimumControlMode)
            _settings.MaximumControlMode = _settings.MinimumControlMode;
        if (_settings.MaximumThermocoupleNumber < _settings.MinimumThermocoupleNumber)
            _settings.MaximumThermocoupleNumber = _settings.MinimumThermocoupleNumber;
        if (_settings.MaximumTemperature < _settings.MinimumTemperature)
            _settings.MaximumTemperature = _settings.MinimumTemperature;
        if (_settings.MaximumFlow < _settings.MinimumFlow)
            _settings.MaximumFlow = _settings.MinimumFlow;
        if (_settings.MaximumTime < _settings.MinimumTime)
            _settings.MaximumTime = _settings.MinimumTime;
    }
    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
    private static void Range(int value, int min, int max, string name, List<string> errors)
    {
        if (value < min || value > max) errors.Add($"{name}应在{min}～{max}之间，当前为{value}");
    }
    private static void Range(float value, float min, float max, string name, List<string> errors)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
            errors.Add($"{name}应在{min:0.##}～{max:0.##}之间，当前为{value}");
    }
}

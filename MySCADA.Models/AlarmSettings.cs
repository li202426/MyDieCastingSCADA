namespace MySCADA.Models;
public sealed class AlarmSettings
{
    public bool CommunicationAlarmEnabled { get; set; } = true;
    public bool TemperatureAlarmEnabled { get; set; } = true;
    public bool FlowAlarmEnabled { get; set; } = true;
    public float AluminiumHighDeviation { get; set; } = 20;
    public float FurnaceHighDeviation { get; set; } = 20;
    public double MinimumFlowRatio { get; set; } = 0.70;
    public double FlowRecoveryRatio { get; set; } = 0.80;
    public float TemperatureRecoveryHysteresis { get; set; } = 5;
    public int TriggerDelaySeconds { get; set; } = 3;
    public int RecoveryDelaySeconds { get; set; } = 3;
}

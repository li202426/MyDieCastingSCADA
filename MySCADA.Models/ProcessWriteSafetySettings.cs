namespace MySCADA.Models;
public sealed class ProcessWriteSafetySettings
{
    public bool WritesEnabled { get; set; } = true;
    public bool AllowWriteWhileInjectionRunning { get; set; } = false;
    public int MaxSnapshotAgeSeconds { get; set; } = 5;
    public int MinimumControlMode { get; set; } = 0;
    public int MaximumControlMode { get; set; } = 2;
    public int MinimumThermocoupleNumber { get; set; } = 0;
    public int MaximumThermocoupleNumber { get; set; } = 32;
    public float MinimumTemperature { get; set; } = 0;
    public float MaximumTemperature { get; set; } = 1000;
    public int MinimumFlow { get; set; } = 0;
    public int MaximumFlow { get; set; } = 10000;
    public int MinimumTime { get; set; } = 0;
    public int MaximumTime { get; set; } = 3600000;
}

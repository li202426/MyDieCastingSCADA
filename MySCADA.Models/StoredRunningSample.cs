namespace MySCADA.Models;
public class StoredRunningSample
{
    public long Id { get; set; }
    public DateTime SampleTime { get; set; }
    public string DeviceName { get; set; } = "";
    public string? CycleId { get; set; }
    public float ActualPressure { get; set; }
    public float ResidualAluminium { get; set; }
    public float AluminiumTemperature { get; set; }
    public float SetAluminiumTemperature { get; set; }
    public float FurnaceTemperature { get; set; }
    public float SetFurnaceTemperature { get; set; }
    public string TemperaturesJson { get; set; } = "[]";
    public string CoolingActualFlowsJson { get; set; } = "[]";
    public string CoolingSetFlowsJson { get; set; } = "[]";
    public string FullSnapshotJson { get; set; } = "{}";
    public string SampleTimeText => SampleTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string CycleIdText => string.IsNullOrWhiteSpace(CycleId) ? "未归属" : CycleId;
}

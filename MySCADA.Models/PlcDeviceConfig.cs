namespace MySCADA.Models
{
    public class VariableAddress
    {
        public string Area { get; set; } = "DB";
        public int DBNumber { get; set; } = 1;
        public int ByteOffset { get; set; } = 0;
        public string DataType { get; set; } = "Real";
        public int Bit { get; set; } = 0;
    }
    public class PlcDeviceConfig
    {
        public VariableAddress InjuctionRun { get; set; } = new();
        public VariableAddress[] Temperatures { get; set; } = Array.Empty<VariableAddress>();
        public VariableAddress ActualPressure { get; set; } = new();
        public VariableAddress TaiBanActualValue { get; set; } = new();
        public VariableAddress TaiBanSetValue { get; set; } = new();
        public VariableAddress AmountOfResidualAluminum { get; set; } = new();
        public VariableAddress ActualAluminiumTemperature { get; set; } = new();
        public VariableAddress SetAluminiumTemperature { get; set; } = new();
        public VariableAddress ActualFurnaceTemperature { get; set; } = new();
        public VariableAddress SetFurnaceTemperature { get; set; } = new();
        public VariableAddress[] CoolingSetFlow { get; set; } = Array.Empty<VariableAddress>();
        public VariableAddress[] CoolingActualFlow { get; set; } = Array.Empty<VariableAddress>();
        public VariableAddress? MouldNumber { get; set; }
        public VariableAddress? WheelTypeNumber { get; set; }
    }
}

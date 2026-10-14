namespace MySCADA.Models
{
        public class PlcRuntimeSnapshot
    {
        public DateTime SampleTime { get; init; } = DateTime.Now;
        public bool IsConnected { get; init; }
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
                public string? CycleId { get; set; }
        public bool InjuctionRun { get; init; }
        public float[] Temperatures { get; init; } = Array.Empty<float>();
        public float ActualPressure { get; init; }
        public float TaiBanActualValue { get; init; }
        public float TaiBanSetValue { get; init; }
        public float AmountOfResidualAluminum { get; init; }
        public float ActualAluminiumTemperature { get; init; }
        public float SetAluminiumTemperature { get; init; }
        public float ActualFurnaceTemperature { get; init; }
        public float SetFurnaceTemperature { get; init; }
        public List<CoolingChannelItem> CoolingChannels { get; init; } = new();
                public List<CoolingChannelItem> ProcessCoolingChannels { get; init; } = new();
        public float[] PressureValues { get; init; } = Array.Empty<float>();
        public int[] TimeValues { get; init; } = Array.Empty<int>();
        public int CoolingTimeMax { get; init; }
        public int CoolingTimeMin { get; init; }
        public float CastingOpenTemperature { get; init; }
        public int CastingOpenMode { get; init; }
        public int CastingOpenThermocoupleNm { get; init; }
    }
}

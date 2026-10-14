using System.Text.Json.Serialization;
namespace MySCADA.Models
{
    public class ProcessVariableAddress : VariableAddress
    {
        public int? WriteByteOffset { get; set; }   
    }
    public class CoolingSubChannel
    {
        public ProcessVariableAddress AuMode { get; set; } = null!;
        public ProcessVariableAddress TheNm { get; set; } = null!;
        public ProcessVariableAddress Temp { get; set; } = null!;
        public ProcessVariableAddress FlowSet { get; set; } = null!;
        public ProcessVariableAddress ConMode { get; set; } = null!;
        public ProcessVariableAddress DelayTime { get; set; } = null!;
        public ProcessVariableAddress OpenTime { get; set; } = null!;
        public ProcessVariableAddress CrossTime { get; set; } = null!;
        public ProcessVariableAddress ActualFlow { get; set; } = null!;
    }
    public class CoolingChannelConfig
    {
        public int DBNumber { get; set; }                     
        public CoolingSubChannel Air { get; set; } = null!;
        public CoolingSubChannel Water { get; set; } = null!;
    }
    public class PlcProcessConfig
    {
        public ProcessVariableAddress[] PressureSet { get; set; } = Array.Empty<ProcessVariableAddress>();   
        public ProcessVariableAddress[] TimeSet { get; set; } = Array.Empty<ProcessVariableAddress>();       
        public ProcessVariableAddress CoolingTimeMax { get; set; } = null!;
        public ProcessVariableAddress CoolingTimeMin { get; set; } = null!;
        public ProcessVariableAddress CastingOpenTemperature { get; set; } = null!;
        public ProcessVariableAddress CastingOpenMode { get; set; } = null!;
        public ProcessVariableAddress CastingOpenThermocoupleNm { get; set; } = null!;
        public ProcessVariableAddress MoldNameString { get; set; } = null!;   
        public CoolingChannelConfig[] CoolingChannels { get; set; } = Array.Empty<CoolingChannelConfig>();
        public ProcessVariableAddress DownloadCastingOpenBlock { get; set; } = null!;
        public ProcessVariableAddress DownloadTimeBlock { get; set; } = null!;
    }
}
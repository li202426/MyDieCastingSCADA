using System;
namespace MySCADA.Models
{
    [Serializable]
    public class DieCastingProcess
    {
        public int ID { get; set; }
        public bool MoldNmFromPLC { get; set; } = false;
        public bool MoldPositionFromPLC { get; set; } = true;
        public string MoldNm { get; set; } = "";
        public string LineNm { get; set; } = "";
        public string MachineNm { get; set; } = "";
        public float[] PressureSet { get; set; } = new float[9];
        public int[] TimeSet { get; set; } = new int[9];
        public int CoolingTimeMax { get; set; }
        public int CoolingTimeMin { get; set; }
        public float CastingOpenTemperature { get; set; }
        public short CastingOpenMode { get; set; }
        public short CastingOpenThermocoupleNm { get; set; }
        public short[] CoolingAuMode { get; set; } = new short[60];
        public short[] CoolingTheNm { get; set; } = new short[60];
        public float[] CoolingTemp { get; set; } = new float[60];
        public float[] CoolingFlowSet { get; set; } = new float[60];
        public short[] CoolingConMode { get; set; } = new short[60];
        public int[] CoolingDelayTime { get; set; } = new int[60];
        public int[] CoolingOpenTime { get; set; } = new int[60];
        public int[] CoolingCrossTime { get; set; } = new int[60];
        public float AluminumTemperatureSet { get; set; }
        public string[] TC { get; set; } = new string[50];
        public string[] MoldCoolingPosition { get; set; } = new string[30];
        public int TimeStampInt { get; set; }
        public string DCPName { get; set; } = "";
    }
}
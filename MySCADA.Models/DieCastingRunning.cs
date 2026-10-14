using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace MySCADA.Models
{
    [Serializable]
        public class DieCastingRunning
    {
        public string MouldNumber { get; set; } = "";
        public string WheelTypeNumber { get; set; } = "";
        public int ID { get; set; }
        public int ProcessID { get; set; } = 0;
        public int MoldID { get; set; } = 0;
        public DateTime SampleTime { get; set; }
        public bool CastingBegin { get; set; }
        public DateTime LastSampleTime { get; set; }
        public string CurveName { get; set; } = "XXXX";
        public int LastInjuctionRun { get; set; } = 1;
        public int InjuctionRun { get; set; } = 0;
        public int OpeningState { get; set; } = 0;
        public float[] T { get; set; } = new float[50] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        public float[] LT { get; set; } = new float[50] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        public float ActualPressure { get; set; } = 0;
        public float ActualAluminiumTemperature { get; set; } = 0;
        public float SetAluminiumTemperature { get; set; } = 0;
        public float OffsetAluminiumTemperature { get; set; } = 0;
        public float ActualFurnaceTemperature { get; set; } = 0;
        public float SetFurnaceTemperature { get; set; } = 0;
        public float OffsetFurnaceTemperature { get; set; } = 0;
        public float[] CoolingSetFlow { get; set; } = new float[60] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        public float[] CoolingActualFlow { get; set; } = new float[60] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        public float AmountOfResidualAluminum { get; set; } = -1;
        public float TaiBanSetValue { get; set; } = -1;
        public float TaiBanAcutalValue { get; set; } = -1;
                public int[] IfTempControll { get; set; } = new int[30] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                public int[] CoolPipeZustand { get; set; } = new int[30] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    }
}

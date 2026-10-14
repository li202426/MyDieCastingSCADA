using MySCADA.Models;
using MySCADA.Services;
using System;
using System.Collections.ObjectModel;
namespace MySCADA.Business
{
        public class DieCastingRunningBLL
    {
        private readonly PlcService _plcService;
        private readonly PlcDeviceConfig _config;
        public DieCastingRunningBLL(PlcService plcService, PlcDeviceConfig config)
        {
            _plcService = plcService ?? throw new ArgumentNullException(nameof(plcService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
                public void UpdateRunningData(
            ObservableCollection<TemperatureItem> temperatures,
            ObservableCollection<CoolingChannelItem> coolingChannels,   
            TemperatureItem actualPressureItem,
            TemperatureItem amountOfResidualAluminum,
            TemperatureItem actualAluminiumTemperature,
            TemperatureItem setAluminiumTemperature,
            TemperatureItem actualFurnaceTemperature,
            TemperatureItem setFurnaceTemperature)
        {
            if (!_plcService.IsConnected) return;
            try
            {
                var temps = _plcService.ReadTemperatures();   
                for (int i = 0; i < temps.Length && i < temperatures.Count; i++)
                {
                    temperatures[i].Value = temps[i];
                }
                actualPressureItem.Value = _plcService.ReadActualPressure();
                amountOfResidualAluminum.Value = _plcService.ReadAmountOfResidualAluminum();
                actualAluminiumTemperature.Value = _plcService.ActualAluminiumTemperature();
                setAluminiumTemperature.Value = _plcService.SetAluminiumTemperature();
                actualFurnaceTemperature.Value = _plcService.ActualFurnaceTemperature();
                setFurnaceTemperature.Value = _plcService.SetFurnaceTemperature();
                var freshChannels = _plcService.ReadAllCoolingChannels();
                coolingChannels.Clear();
                foreach (var ch in freshChannels)
                {
                    coolingChannels.Add(ch);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DieCastingRunningBLL] 更新实时数据异常: {ex.Message}");
            }
        }
    }
}
using MySCADA.Models;
using MySCADA.Services;
using System;
using System.Collections.ObjectModel;
namespace MySCADA.Business
{
        public class DieCastingProcessBLL
    {
        private readonly PlcService _plcService;
        private readonly PlcProcessConfig _processConfig;
        private readonly ProcessWriteSafetyService _writeSafety;
        private readonly Func<PlcRuntimeSnapshot?> _latestSnapshotProvider;
        public string? LastErrorMessage { get; private set; }
        public DieCastingProcessBLL(
            PlcService plcService,
            PlcProcessConfig processConfig,
            ProcessWriteSafetyService writeSafety,
            Func<PlcRuntimeSnapshot?> latestSnapshotProvider)
        {
            _plcService = plcService ?? throw new ArgumentNullException(nameof(plcService));
            _processConfig = processConfig ?? throw new ArgumentNullException(nameof(processConfig));
            _writeSafety = writeSafety ?? throw new ArgumentNullException(nameof(writeSafety));
            _latestSnapshotProvider = latestSnapshotProvider ??
                throw new ArgumentNullException(nameof(latestSnapshotProvider));
        }
                public void UpdateProcessData(
            ObservableCollection<PressureItem> pressureItems,
            ObservableCollection<TimeItem> timeItems,
            TemperatureItem coolingTimeMaxItem,
            TemperatureItem coolingTimeMinItem,
            TemperatureItem castingOpenTempItem,
            TemperatureItem castingOpenModeItem,
            TemperatureItem castingOpenThermoItem)
        {
            if (!_plcService.IsConnected) return;
            try
            {
                for (int i = 0; i < _processConfig.PressureSet.Length && i < pressureItems.Count; i++)
                {
                    pressureItems[i].Value = _plcService.ReadFloat(_processConfig.PressureSet[i]);
                }
                for (int i = 0; i < _processConfig.TimeSet.Length && i < timeItems.Count; i++)
                {
                    timeItems[i].Value = _plcService.ReadDInt(_processConfig.TimeSet[i]);
                }
                coolingTimeMaxItem.Value = _plcService.ReadDInt(_processConfig.CoolingTimeMax);
                coolingTimeMinItem.Value = _plcService.ReadDInt(_processConfig.CoolingTimeMin);
                castingOpenTempItem.Value = _plcService.ReadFloat(_processConfig.CastingOpenTemperature);
                castingOpenModeItem.Value = _plcService.ReadInt(_processConfig.CastingOpenMode);
                castingOpenThermoItem.Value = _plcService.ReadInt(_processConfig.CastingOpenThermocoupleNm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DieCastingProcessBLL] 更新工艺数据异常: {ex.Message}");
            }
        }
                public bool DownloadSingleChannel(int channelIndex, EditableChannelItem editItem)
        {
            if (channelIndex < 1 || channelIndex > _processConfig.CoolingChannels.Length || editItem == null)
            {
                LastErrorMessage = $"冷却通道编号无效：{channelIndex}。";
                return false;
            }
            if (!CanStartWrite()) return false;
            if (!_writeSafety.ValidateChannel(editItem, out string validationError))
            {
                LastErrorMessage = validationError;
                return false;
            }
            bool success = _plcService.ExecuteExclusive(
                () => _plcService.DownloadCoolingSingleChannel(channelIndex, editItem));
            LastErrorMessage = success
                ? null
                : $"通道 {channelIndex} 下发失败：{_plcService.LastErrorMessage ?? "未知错误"}";
            return success;
        }
                public bool DownloadAllChannels(ObservableCollection<EditableChannelItem> editableChannels)
        {
            if (editableChannels == null || editableChannels.Count == 0)
            {
                LastErrorMessage = "没有可以下发的冷却通道。";
                return false;
            }
            if (!CanStartWrite()) return false;
            for (int i = 0; i < editableChannels.Count; i++)
            {
                if (_writeSafety.ValidateChannel(editableChannels[i], out string validationError))
                    continue;
                LastErrorMessage = validationError;
                return false;
            }
            bool allSuccess = _plcService.ExecuteExclusive(() =>
            {
                for (int i = 0; i < editableChannels.Count; i++)
                {
                    bool success = _plcService.DownloadCoolingSingleChannel(
                        i + 1, editableChannels[i]);
                    if (!success)
                    {
                        LastErrorMessage = $"全部下发在通道 {i + 1} 处停止：" +
                            (_plcService.LastErrorMessage ?? "PLC未返回详细错误信息");
                        return false;
                    }
                }
                return true;
            });
            if (!allSuccess) return false;
            LastErrorMessage = null;
            return true;
        }
        public bool DownloadOpenParam(int mode, float temp, int thermo)
        {
            if (!CanStartWrite()) return false;
            if (!_writeSafety.ValidateOpenParameters(mode, temp, thermo, out string validationError))
            {
                LastErrorMessage = validationError;
                return false;
            }
            bool success = _plcService.ExecuteExclusive(
                () => _plcService.WriteOpenParam(mode, temp, thermo));
            LastErrorMessage = success ? null : _plcService.LastErrorMessage;
            return success;
        }
        public bool DownloadMaintainCoolTime(int maintainTime, float coolMax, float coolMin)
        {
            if (!CanStartWrite()) return false;
            if (!_writeSafety.ValidateMaintainParameters(
                    maintainTime, coolMax, coolMin, out string validationError))
            {
                LastErrorMessage = validationError;
                return false;
            }
            bool success = _plcService.ExecuteExclusive(
                () => _plcService.WriteMaintainCoolTime(maintainTime, coolMax, coolMin));
            LastErrorMessage = success ? null : _plcService.LastErrorMessage;
            return success;
        }
        private bool CanStartWrite()
        {
            if (!AuthorizationService.Require(AppPermission.ManageProcess, out string permissionError))
            {
                LastErrorMessage = permissionError;
                return false;
            }
            if (_writeSafety.ValidateWriteState(
                    _plcService.IsConnected, _latestSnapshotProvider(), out string error))
                return true;
            LastErrorMessage = error;
            return false;
        }
    }
}

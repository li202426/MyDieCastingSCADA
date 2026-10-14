using S7.Net;
using MySCADA.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
namespace MySCADA.Services
{
    public class PlcService : IDisposable
    {
        private Plc? _plc;
        private string? _ipAddress;
        private readonly object _communicationLock = new();
        private readonly PlcDeviceConfig _deviceConfig;   
        private readonly PlcProcessConfig _processConfig; 
        public string? LastErrorMessage { get; private set; }
        public PlcService(PlcDeviceConfig deviceConfig, PlcProcessConfig processConfig)
        {
            _deviceConfig = deviceConfig ?? throw new ArgumentNullException(nameof(deviceConfig));
            _processConfig = processConfig ?? throw new ArgumentNullException(nameof(processConfig));
        }
        public bool Connect(string ipAddress)
        {
            lock (_communicationLock)
            {
                _ipAddress = ipAddress;
                try
                {
                    _plc?.Close();
                    _plc = new Plc(CpuType.S71500, ipAddress, 0, 1);
                    _plc.Open();
                    return _plc.IsConnected;
                }
                catch (Exception ex)
                {
                    try { _plc?.Close(); } catch { }
                    _plc = null;
                    Console.WriteLine($"[PlcService] 连接失败: {ex.Message}");
                    return false;
                }
            }
        }
        public bool TryReconnect()
        {
            string? ipAddress;
            lock (_communicationLock)
                ipAddress = _ipAddress;
            return !string.IsNullOrWhiteSpace(ipAddress) && Connect(ipAddress);
        }
        public bool IsConnected
        {
            get
            {
                lock (_communicationLock)
                    return _plc?.IsConnected == true;
            }
        }
        public T ExecuteExclusive<T>(Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            lock (_communicationLock)
                return operation();
        }
        #region ==================== 通用读取方法 ====================
        private static DataType GetDataType(VariableAddress addr)
        {
            return addr.Area.Trim().ToUpperInvariant() switch
            {
                "DB" => DataType.DataBlock,
                "M" => DataType.Memory,
                "I" => DataType.Input,
                "Q" => DataType.Output,
                _ => throw new ArgumentException($"不支持的 PLC 区域: {addr.Area}")
            };
        }
        private static int GetDbNumber(VariableAddress addr)
            => GetDataType(addr) == DataType.DataBlock ? addr.DBNumber : 0;
        public float ReadFloat(VariableAddress addr)
        {
            lock (_communicationLock)
            {
                if (addr == null) throw new ArgumentNullException(nameof(addr));
                if (!IsConnected) throw new InvalidOperationException("PLC 未连接，无法读取 REAL。");
                try
                {
                    byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 4);
                    Array.Reverse(bytes);                    
                    return BitConverter.ToSingle(bytes, 0);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"读取 REAL 失败（{FormatAddress(addr)}）：{ex.Message}", ex);
                }
            }
        }
        public int ReadInt(VariableAddress addr)
        {
            lock (_communicationLock)
            {
                if (addr == null) throw new ArgumentNullException(nameof(addr));
                if (!IsConnected) throw new InvalidOperationException("PLC 未连接，无法读取 INT。");
                try
                {
                    byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 2);
                    Array.Reverse(bytes);
                    return BitConverter.ToInt16(bytes, 0);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"读取 INT 失败（{FormatAddress(addr)}）：{ex.Message}", ex);
                }
            }
        }
        public int ReadDInt(VariableAddress addr)
        {
            lock (_communicationLock)
            {
                if (addr == null) throw new ArgumentNullException(nameof(addr));
                if (!IsConnected) throw new InvalidOperationException("PLC 未连接，无法读取 DINT。");
                try
                {
                    byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 4);
                    Array.Reverse(bytes);
                    return BitConverter.ToInt32(bytes, 0);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"读取 DINT 失败（{FormatAddress(addr)}）：{ex.Message}", ex);
                }
            }
        }
        public bool ReadBool(VariableAddress addr)
        {
            lock (_communicationLock)
            {
                if (addr == null) throw new ArgumentNullException(nameof(addr));
                if (!IsConnected) throw new InvalidOperationException("PLC 未连接，无法读取 BOOL。");
                try
                {
                    if (addr.Bit is < 0 or > 7)
                        throw new ArgumentOutOfRangeException(nameof(addr.Bit), "位号必须是 0 到 7");
                    var bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 1);
                    return (bytes[0] & (1 << addr.Bit)) != 0;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"读取 BOOL 失败（{FormatAddress(addr)}.{addr.Bit}）：{ex.Message}", ex);
                }
            }
        }
        #endregion
        #region ==================== 实时运行数据辅助方法（供 DieCastingRunningBLL 调用） ====================
        public float[] ReadTemperatures()
        {
            if (!IsConnected || _deviceConfig.Temperatures == null) return Array.Empty<float>();
            var result = new float[_deviceConfig.Temperatures.Length];
            for (int i = 0; i < _deviceConfig.Temperatures.Length; i++)
            {
                result[i] = ReadFloat(_deviceConfig.Temperatures[i]);
            }
            return result;
        }
        public float ReadActualPressure() => ReadFloat(_deviceConfig.ActualPressure);
        public float ReadAmountOfResidualAluminum() => ReadFloat(_deviceConfig.AmountOfResidualAluminum);
        public float ActualAluminiumTemperature() => ReadFloat(_deviceConfig.ActualAluminiumTemperature);
        public float SetAluminiumTemperature() => ReadFloat(_deviceConfig.SetAluminiumTemperature);
        public float ActualFurnaceTemperature() => ReadFloat(_deviceConfig.ActualFurnaceTemperature);
        public float SetFurnaceTemperature() => ReadFloat(_deviceConfig.SetFurnaceTemperature);
                public PlcRuntimeSnapshot ReadRuntimeSnapshot()
        {
            lock (_communicationLock)
            {
                bool connected = IsConnected;
                if (!connected)
                {
                    return new PlcRuntimeSnapshot
                    {
                        SampleTime = DateTime.Now,
                        IsConnected = false,
                        IsSuccess = false,
                        ErrorMessage = "PLC 未连接。"
                    };
                }
                var addresses = new List<VariableAddress>();
                void AddAddress(VariableAddress? address)
                {
                    if (address != null) addresses.Add(address);
                }
                AddAddress(_deviceConfig.InjuctionRun);
                foreach (var address in _deviceConfig.Temperatures) AddAddress(address);
                AddAddress(_deviceConfig.ActualPressure);
                AddAddress(_deviceConfig.TaiBanActualValue);
                AddAddress(_deviceConfig.TaiBanSetValue);
                AddAddress(_deviceConfig.AmountOfResidualAluminum);
                AddAddress(_deviceConfig.ActualAluminiumTemperature);
                AddAddress(_deviceConfig.SetAluminiumTemperature);
                AddAddress(_deviceConfig.ActualFurnaceTemperature);
                AddAddress(_deviceConfig.SetFurnaceTemperature);
                foreach (var address in _deviceConfig.CoolingSetFlow) AddAddress(address);
                foreach (var address in _deviceConfig.CoolingActualFlow) AddAddress(address);
                foreach (var address in _processConfig.PressureSet) AddAddress(address);
                foreach (var address in _processConfig.TimeSet) AddAddress(address);
                AddAddress(_processConfig.CoolingTimeMax);
                AddAddress(_processConfig.CoolingTimeMin);
                AddAddress(_processConfig.CastingOpenTemperature);
                AddAddress(_processConfig.CastingOpenMode);
                AddAddress(_processConfig.CastingOpenThermocoupleNm);
                foreach (var channel in _processConfig.CoolingChannels)
                {
                    if (channel.Air != null)
                    {
                        AddAddress(channel.Air.AuMode);
                        AddAddress(channel.Air.TheNm);
                        AddAddress(channel.Air.Temp);
                        AddAddress(channel.Air.FlowSet);
                        AddAddress(channel.Air.ConMode);
                        AddAddress(channel.Air.DelayTime);
                        AddAddress(channel.Air.OpenTime);
                        AddAddress(channel.Air.CrossTime);
                    }
                    if (channel.Water != null)
                    {
                        AddAddress(channel.Water.AuMode);
                        AddAddress(channel.Water.TheNm);
                        AddAddress(channel.Water.Temp);
                        AddAddress(channel.Water.FlowSet);
                        AddAddress(channel.Water.ConMode);
                        AddAddress(channel.Water.DelayTime);
                        AddAddress(channel.Water.OpenTime);
                        AddAddress(channel.Water.CrossTime);
                    }
                }
                BatchReadResult batchResult = ReadBatchValues(addresses);
                if (!batchResult.IsSuccess)
                {
                    try { _plc?.Close(); } catch { }
                    _plc = null;
                    return new PlcRuntimeSnapshot
                    {
                        SampleTime = DateTime.Now,
                        IsConnected = false,
                        IsSuccess = false,
                        ErrorMessage = string.Join("；", batchResult.Errors)
                    };
                }
                Dictionary<VariableAddress, object> values = batchResult.Values;
                float[] temperatures = _deviceConfig.Temperatures
                    .Select(address => GetFloatValue(values, address))
                    .ToArray();
                float[] pressureValues = _processConfig.PressureSet
                    .Select(address => GetFloatValue(values, address))
                    .ToArray();
                int[] timeValues = _processConfig.TimeSet
                    .Select(address => GetIntValue(values, address))
                    .ToArray();
                var coolingChannels = new List<CoolingChannelItem>();
                var processCoolingChannels = new List<CoolingChannelItem>();
                int totalSet = _deviceConfig.CoolingSetFlow.Length;
                int halfSet = totalSet / 2;
                int totalActual = _deviceConfig.CoolingActualFlow.Length;
                int halfActual = totalActual / 2;
                for (int i = 0; i < _processConfig.CoolingChannels.Length; i++)
                {
                    var config = _processConfig.CoolingChannels[i];
                    var item = new CoolingChannelItem { Channel = i + 1 };
                    if (config.Air != null)
                    {
                        item.AirAuMode = GetIntValue(values, config.Air.AuMode);
                        item.AirTheNm = GetIntValue(values, config.Air.TheNm);
                        item.AirTemp = GetFloatValue(values, config.Air.Temp);
                        item.AirFlowSet = GetIntValue(values, config.Air.FlowSet);
                        item.AirConMode = GetIntValue(values, config.Air.ConMode);
                        item.AirDelayTime = GetIntValue(values, config.Air.DelayTime);
                        item.AirOpenTime = GetIntValue(values, config.Air.OpenTime);
                        item.AirCrossTime = GetIntValue(values, config.Air.CrossTime);
                    }
                    if (config.Water != null)
                    {
                        item.WaterAuMode = GetIntValue(values, config.Water.AuMode);
                        item.WaterTheNm = GetIntValue(values, config.Water.TheNm);
                        item.WaterTemp = GetFloatValue(values, config.Water.Temp);
                        item.WaterFlowSet = (int)GetFloatValue(values, config.Water.FlowSet);
                        item.WaterConMode = GetIntValue(values, config.Water.ConMode);
                        item.WaterDelayTime = GetIntValue(values, config.Water.DelayTime);
                        item.WaterOpenTime = GetIntValue(values, config.Water.OpenTime);
                        item.WaterCrossTime = GetIntValue(values, config.Water.CrossTime);
                    }
                    processCoolingChannels.Add(CloneCoolingChannel(item));
                    int airSetIndex = i;
                    int waterSetIndex = i + halfSet;
                    item.AirFlowSet = airSetIndex < totalSet
                        ? GetIntValue(values, _deviceConfig.CoolingSetFlow[airSetIndex])
                        : 0;
                    item.WaterFlowSet = waterSetIndex < totalSet
                        ? GetIntValue(values, _deviceConfig.CoolingSetFlow[waterSetIndex])
                        : 0;
                    int airActualIndex = i;
                    int waterActualIndex = i + halfActual;
                    item.AirActual = airActualIndex < totalActual
                        ? GetIntValue(values, _deviceConfig.CoolingActualFlow[airActualIndex])
                        : null;
                    item.WaterActual = waterActualIndex < totalActual
                        ? GetIntValue(values, _deviceConfig.CoolingActualFlow[waterActualIndex])
                        : null;
                    coolingChannels.Add(item);
                }
                return new PlcRuntimeSnapshot
                {
                    SampleTime = DateTime.Now,
                    IsConnected = true,
                    IsSuccess = true,
                    InjuctionRun = GetBoolValue(values, _deviceConfig.InjuctionRun),
                    Temperatures = temperatures,
                    ActualPressure = GetFloatValue(values, _deviceConfig.ActualPressure),
                    TaiBanActualValue = GetFloatValue(values, _deviceConfig.TaiBanActualValue),
                    TaiBanSetValue = GetFloatValue(values, _deviceConfig.TaiBanSetValue),
                    AmountOfResidualAluminum = GetFloatValue(values, _deviceConfig.AmountOfResidualAluminum),
                    ActualAluminiumTemperature = GetFloatValue(values, _deviceConfig.ActualAluminiumTemperature),
                    SetAluminiumTemperature = GetFloatValue(values, _deviceConfig.SetAluminiumTemperature),
                    ActualFurnaceTemperature = GetFloatValue(values, _deviceConfig.ActualFurnaceTemperature),
                    SetFurnaceTemperature = GetFloatValue(values, _deviceConfig.SetFurnaceTemperature),
                    CoolingChannels = coolingChannels,
                    ProcessCoolingChannels = processCoolingChannels,
                    PressureValues = pressureValues,
                    TimeValues = timeValues,
                    CoolingTimeMax = GetIntValue(values, _processConfig.CoolingTimeMax),
                    CoolingTimeMin = GetIntValue(values, _processConfig.CoolingTimeMin),
                    CastingOpenTemperature = GetFloatValue(values, _processConfig.CastingOpenTemperature),
                    CastingOpenMode = GetIntValue(values, _processConfig.CastingOpenMode),
                    CastingOpenThermocoupleNm = GetIntValue(values, _processConfig.CastingOpenThermocoupleNm)
                };
            }
        }
        private sealed class BatchReadResult
        {
            public Dictionary<VariableAddress, object> Values { get; } = new();
            public List<string> Errors { get; } = new();
            public bool IsSuccess => Errors.Count == 0;
        }
        private BatchReadResult ReadBatchValues(IEnumerable<VariableAddress> addresses)
        {
            var result = new BatchReadResult();
            var groups = addresses
                .Distinct()
                .GroupBy(address => new
                {
                    Area = address.Area.Trim().ToUpperInvariant(),
                    DBNumber = address.Area.Equals("DB", StringComparison.OrdinalIgnoreCase)
                        ? address.DBNumber
                        : 0
                });
            foreach (var group in groups)
            {
                VariableAddress first = group.First();
                int startOffset = group.Min(address => address.ByteOffset);
                int endOffset = group.Max(address => address.ByteOffset + GetByteLength(address));
                try
                {
                    byte[] buffer = _plc!.ReadBytes(
                        GetDataType(first), group.Key.DBNumber, startOffset, endOffset - startOffset);
                    foreach (var address in group)
                    {
                        int relativeOffset = address.ByteOffset - startOffset;
                        result.Values[address] = ParseBatchValue(buffer, relativeOffset, address);
                    }
                }
                catch (Exception ex)
                {
                    string error =
                        $"批量读取失败（{group.Key.Area}, DB={group.Key.DBNumber}, " +
                        $"Offset={startOffset}, Length={endOffset - startOffset}）：{ex.Message}";
                    result.Errors.Add(error);
                    Console.WriteLine($"[PlcService] {error}");
                }
            }
            return result;
        }
        private static int GetByteLength(VariableAddress address)
        {
            return address.DataType.Trim().ToUpperInvariant() switch
            {
                "BOOL" => 1,
                "INT" => 2,
                "DINT" => 4,
                "REAL" => 4,
                _ => throw new NotSupportedException(
                    $"批量读取暂不支持数据类型：{address.DataType}")
            };
        }
        private static object ParseBatchValue(byte[] buffer, int offset, VariableAddress address)
        {
            ReadOnlySpan<byte> span = buffer.AsSpan(offset);
            return address.DataType.Trim().ToUpperInvariant() switch
            {
                "BOOL" => (span[0] & (1 << address.Bit)) != 0,
                "INT" => (int)BinaryPrimitives.ReadInt16BigEndian(span),
                "DINT" => BinaryPrimitives.ReadInt32BigEndian(span),
                "REAL" => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(span)),
                _ => throw new NotSupportedException(
                    $"批量读取暂不支持数据类型：{address.DataType}")
            };
        }
        private static float GetFloatValue(
            IReadOnlyDictionary<VariableAddress, object> values, VariableAddress address)
        {
            return values.TryGetValue(address, out object? value) && value is float number
                ? number
                : throw new System.IO.InvalidDataException($"快照缺少 REAL 地址：{FormatAddress(address)}");
        }
        private static int GetIntValue(
            IReadOnlyDictionary<VariableAddress, object> values, VariableAddress address)
        {
            return values.TryGetValue(address, out object? value) && value is int number
                ? number
                : throw new System.IO.InvalidDataException($"快照缺少整数地址：{FormatAddress(address)}");
        }
        private static bool GetBoolValue(
            IReadOnlyDictionary<VariableAddress, object> values, VariableAddress address)
        {
            return values.TryGetValue(address, out object? value) && value is bool flag
                ? flag
                : throw new System.IO.InvalidDataException($"快照缺少 BOOL 地址：{FormatAddress(address)}");
        }
        private static CoolingChannelItem CloneCoolingChannel(CoolingChannelItem source) => new()
        {
            Channel=source.Channel,
            AirFlowSet=source.AirFlowSet, WaterFlowSet=source.WaterFlowSet,
            AirAuMode=source.AirAuMode, AirTheNm=source.AirTheNm, AirTemp=source.AirTemp,
            AirConMode=source.AirConMode, AirDelayTime=source.AirDelayTime,
            AirOpenTime=source.AirOpenTime, AirCrossTime=source.AirCrossTime,
            WaterAuMode=source.WaterAuMode, WaterTheNm=source.WaterTheNm, WaterTemp=source.WaterTemp,
            WaterConMode=source.WaterConMode, WaterDelayTime=source.WaterDelayTime,
            WaterOpenTime=source.WaterOpenTime, WaterCrossTime=source.WaterCrossTime
        };
        #endregion
        #region ==================== 动态冷却通道读取（核心） ====================
                public List<CoolingChannelItem> ReadAllCoolingChannels()
        {
            var result = new List<CoolingChannelItem>();
            if (!IsConnected || _processConfig.CoolingChannels == null)
                return result;
            int totalSet = _deviceConfig.CoolingSetFlow?.Length ?? 0;
            int halfSet = totalSet / 2;
            for (int i = 0; i < _processConfig.CoolingChannels.Length; i++)
            {
                var ch = _processConfig.CoolingChannels[i];
                var item = new CoolingChannelItem { Channel = i + 1 };
                if (ch.Air != null)
                {
                    item.AirAuMode = ReadInt(ch.Air.AuMode);
                    item.AirTheNm = ReadInt(ch.Air.TheNm);
                    item.AirTemp = ReadFloat(ch.Air.Temp);
                    item.AirConMode = ReadInt(ch.Air.ConMode);
                    item.AirDelayTime = ReadDInt(ch.Air.DelayTime);
                    item.AirOpenTime = ReadDInt(ch.Air.OpenTime);
                    item.AirCrossTime = ReadDInt(ch.Air.CrossTime);
                }
                if (ch.Water != null)
                {
                    item.WaterAuMode = ReadInt(ch.Water.AuMode);
                    item.WaterTheNm = ReadInt(ch.Water.TheNm);
                    item.WaterTemp = ReadFloat(ch.Water.Temp);
                    item.WaterConMode = ReadInt(ch.Water.ConMode);
                    item.WaterDelayTime = ReadDInt(ch.Water.DelayTime);
                    item.WaterOpenTime = ReadDInt(ch.Water.OpenTime);
                    item.WaterCrossTime = ReadDInt(ch.Water.CrossTime);
                }
                int airSetIndex = i;
                int waterSetIndex = i + halfSet;
                item.AirFlowSet = airSetIndex < totalSet
                    ? ReadInt(_deviceConfig.CoolingSetFlow[airSetIndex])
                    : 0;
                item.WaterFlowSet = waterSetIndex < totalSet
                    ? ReadInt(_deviceConfig.CoolingSetFlow[waterSetIndex])
                    : 0;
                int totalActual = _deviceConfig.CoolingActualFlow?.Length ?? 0;
                int half = totalActual / 2;           
                int airIndex = i;
                int waterIndex = i + half;
                if (airIndex < totalActual)
                    item.AirActual = ReadInt(_deviceConfig.CoolingActualFlow[airIndex]);
                else
                    item.AirActual = null;
                if (waterIndex < totalActual)
                    item.WaterActual = ReadInt(_deviceConfig.CoolingActualFlow[waterIndex]);
                else
                    item.WaterActual = null;
                result.Add(item);
            }
            return result;
        }
        #endregion
        #region ==================== 写入方法（支持工艺下发） ====================
        public bool WriteFloat(VariableAddress addr, float value)
        {
            lock (_communicationLock)
            {
                if (!IsConnected)
                    return SetWriteError("PLC 未连接，REAL 写入已取消。");
                if (addr == null)
                    return SetWriteError("REAL 写入地址为空。");
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return SetWriteError($"REAL 写入值无效：{value}。");
                try
                {
                    byte[] bytes = BitConverter.GetBytes(value);
                    Array.Reverse(bytes);                    
                    _plc!.WriteBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, bytes);
                    LastErrorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    return SetWriteError($"写入 REAL 失败（{FormatAddress(addr)}，值={value}）：{ex.Message}");
                }
            }
        }
        public bool WriteInt(VariableAddress addr, int value)
        {
            lock (_communicationLock)
            {
                if (!IsConnected)
                    return SetWriteError("PLC 未连接，INT 写入已取消。");
                if (addr == null)
                    return SetWriteError("INT 写入地址为空。");
                if (value < short.MinValue || value > short.MaxValue)
                    return SetWriteError($"INT 写入值超出范围（-32768～32767）：{value}。");
                try
                {
                    byte[] bytes = BitConverter.GetBytes((short)value);
                    Array.Reverse(bytes);
                    _plc!.WriteBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, bytes);
                    LastErrorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    return SetWriteError($"写入 INT 失败（{FormatAddress(addr)}，值={value}）：{ex.Message}");
                }
            }
        }
        public bool WriteDInt(VariableAddress addr, int value)
        {
            lock (_communicationLock)
            {
                if (!IsConnected)
                    return SetWriteError("PLC 未连接，DINT 写入已取消。");
                if (addr == null)
                    return SetWriteError("DINT 写入地址为空。");
                try
                {
                    byte[] bytes = BitConverter.GetBytes(value);
                    Array.Reverse(bytes);
                    _plc!.WriteBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, bytes);
                    LastErrorMessage = null;
                    return true;
                }
                catch (Exception ex)
                {
                    return SetWriteError($"写入 DINT 失败（{FormatAddress(addr)}，值={value}）：{ex.Message}");
                }
            }
        }
        private bool SetWriteError(string message)
        {
            LastErrorMessage = message;
            Console.WriteLine($"[PlcService] {message}");
            return false;
        }
        private static string FormatAddress(VariableAddress addr)
        {
            string area = addr.Area.Trim().ToUpperInvariant();
            return area == "DB"
                ? $"DB{addr.DBNumber}.DBB{addr.ByteOffset}"
                : $"{area}{addr.ByteOffset}";
        }
                public bool WriteAndVerifyFloat(VariableAddress addr, float value, float tolerance = 0.001f)
        {
            if (!WriteFloat(addr, value)) return false;
            try
            {
                byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 4);
                Array.Reverse(bytes);
                float actual = BitConverter.ToSingle(bytes, 0);
                if (float.IsNaN(actual) || Math.Abs(actual - value) > tolerance)
                {
                    return SetWriteError(
                        $"REAL 写入后校验失败（{FormatAddress(addr)}）：期望={value}，回读={actual}。");
                }
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"REAL 写入后回读失败（{FormatAddress(addr)}）：{ex.Message}");
            }
        }
                public bool WriteAndVerifyInt(VariableAddress addr, int value)
        {
            if (!WriteInt(addr, value)) return false;
            try
            {
                byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 2);
                Array.Reverse(bytes);
                int actual = BitConverter.ToInt16(bytes, 0);
                if (actual != value)
                {
                    return SetWriteError(
                        $"INT 写入后校验失败（{FormatAddress(addr)}）：期望={value}，回读={actual}。");
                }
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"INT 写入后回读失败（{FormatAddress(addr)}）：{ex.Message}");
            }
        }
                public bool WriteAndVerifyDInt(VariableAddress addr, int value)
        {
            if (!WriteDInt(addr, value)) return false;
            try
            {
                byte[] bytes = _plc!.ReadBytes(GetDataType(addr), GetDbNumber(addr), addr.ByteOffset, 4);
                Array.Reverse(bytes);
                int actual = BitConverter.ToInt32(bytes, 0);
                if (actual != value)
                {
                    return SetWriteError(
                        $"DINT 写入后校验失败（{FormatAddress(addr)}）：期望={value}，回读={actual}。");
                }
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"DINT 写入后回读失败（{FormatAddress(addr)}）：{ex.Message}");
            }
        }
        public bool WriteFloat(ProcessVariableAddress addr, float value)
            => WriteFloat((VariableAddress)addr, value);
        public bool WriteInt(ProcessVariableAddress addr, int value)
            => WriteInt((VariableAddress)addr, value);
        public bool WriteDInt(ProcessVariableAddress addr, int value)
            => WriteDInt((VariableAddress)addr, value);
        #endregion
        #region ==================== 工艺下发专用方法（单通道） ====================
                public bool DownloadCoolingSingleChannel(int channelIndex, EditableChannelItem editItem)
        {
            if (channelIndex < 1 || channelIndex > _processConfig.CoolingChannels.Length || editItem == null)
                return SetWriteError($"冷却通道编号无效：{channelIndex}。");
            var ch = _processConfig.CoolingChannels[channelIndex - 1];
            try
            {
                if (ch.Air != null)
                {
                    if (!WriteAndVerifyInt(ch.Air.ConMode, editItem.AirConMode)) return false;
                    if (!WriteAndVerifyInt(ch.Air.TheNm, editItem.AirTheNm)) return false;
                    if (!WriteAndVerifyFloat(ch.Air.Temp, editItem.AirSetTemp)) return false;
                    if (!WriteAndVerifyInt(ch.Air.FlowSet, editItem.AirFlow)) return false;
                    if (!WriteAndVerifyDInt(ch.Air.DelayTime, editItem.AirLastTime)) return false;
                    if (!WriteAndVerifyDInt(ch.Air.OpenTime, editItem.AirOpenTime)) return false;
                    if (!WriteAndVerifyDInt(ch.Air.CrossTime, editItem.AirCrossTime)) return false;
                }
                if (ch.Water != null)
                {
                    if (!WriteAndVerifyInt(ch.Water.ConMode, editItem.WaterConMode)) return false;
                    if (!WriteAndVerifyInt(ch.Water.TheNm, editItem.WaterTheNm)) return false;
                    if (!WriteAndVerifyFloat(ch.Water.Temp, editItem.WaterSetTemp)) return false;
                    if (!WriteAndVerifyFloat(ch.Water.FlowSet, editItem.WaterFlow)) return false;
                    if (!WriteAndVerifyDInt(ch.Water.DelayTime, editItem.WaterLastTime)) return false;
                    if (!WriteAndVerifyDInt(ch.Water.OpenTime, editItem.WaterOpenTime)) return false;
                    if (!WriteAndVerifyDInt(ch.Water.CrossTime, editItem.WaterCrossTime)) return false;
                }
                LastErrorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"通道 {channelIndex} 下发异常：{ex.Message}");
            }
        }
        #endregion
        #region ==================== 开模参数 + 保压冷却时间写入 ====================
        public bool WriteOpenParam(int mode, float temp, int thermo)
        {
            try
            {
                if (!WriteAndVerifyInt(_processConfig.CastingOpenMode, mode)) return false;
                if (!WriteAndVerifyFloat(_processConfig.CastingOpenTemperature, temp)) return false;
                if (!WriteAndVerifyInt(_processConfig.CastingOpenThermocoupleNm, thermo)) return false;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"开模参数下发异常：{ex.Message}");
            }
        }
        public bool WriteMaintainCoolTime(int maintainTime, float coolMax, float coolMin)
        {
            try
            {
                if (_processConfig.TimeSet.Length <= 8)
                    return SetWriteError("TimeSet 缺少保压时间地址（索引 8）。");
                if (!WriteAndVerifyDInt(_processConfig.TimeSet[8], maintainTime)) return false;
                if (!WriteAndVerifyDInt(_processConfig.CoolingTimeMax, (int)coolMax)) return false;
                if (!WriteAndVerifyDInt(_processConfig.CoolingTimeMin, (int)coolMin)) return false;
                return true;
            }
            catch (Exception ex)
            {
                return SetWriteError($"保压冷却时间下发异常：{ex.Message}");
            }
        }
        #endregion
        public void Dispose()
        {
            lock (_communicationLock)
            {
                try
                {
                    _plc?.Close();
                    _plc = null;
                }
                catch { }
            }
        }
    }
}

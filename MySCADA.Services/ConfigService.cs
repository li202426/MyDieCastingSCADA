using MySCADA.Models;
using System.Text.Json;
public class ConfigService
{
    private readonly string _configPath;
    private readonly string _processConfigPath;   
    public ConfigService(string configKey)
    {
        if (string.IsNullOrWhiteSpace(configKey))
            throw new ArgumentException("设备配置编号不能为空，例如 L1-A1。", nameof(configKey));
        string safeConfigKey = Path.GetFileName(configKey.Trim());
        string configFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        _configPath = Path.Combine(configFolder, $"{safeConfigKey}.json");
        _processConfigPath = Path.Combine(configFolder, $"{safeConfigKey}_Process.json");
    }
    public PlcDeviceConfig LoadDeviceConfig()
    {
        if (!File.Exists(_configPath))
            throw new FileNotFoundException($"基础配置文件未找到: {_configPath}");
        string json = File.ReadAllText(_configPath);
        PlcDeviceConfig config = JsonSerializer.Deserialize<PlcDeviceConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"基础配置内容为空或格式错误: {_configPath}");
        ValidateDeviceConfig(config, _configPath);
        return config;
    }
    public PlcProcessConfig LoadProcessConfig()
    {
        if (!File.Exists(_processConfigPath))
            throw new FileNotFoundException(
                $"工艺配置文件未找到: {_processConfigPath}\n" +
                "请为该设备提供正确的工艺点位文件，不要复制其他产线的 PLC 地址配置。",
                _processConfigPath);
        string json = File.ReadAllText(_processConfigPath);
        PlcProcessConfig config = JsonSerializer.Deserialize<PlcProcessConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"工艺配置内容为空或格式错误: {_processConfigPath}");
        NormalizeCoolingChannelDbNumbers(config);
        ValidateProcessConfig(config, _processConfigPath);
        return config;
    }
        private static void NormalizeCoolingChannelDbNumbers(PlcProcessConfig config)
    {
        foreach (CoolingChannelConfig? channel in config.CoolingChannels ?? Array.Empty<CoolingChannelConfig>())
        {
            if (channel == null) continue;
            ApplyDbNumber(channel.Air, channel.DBNumber);
            ApplyDbNumber(channel.Water, channel.DBNumber);
        }
    }
    private static void ApplyDbNumber(CoolingSubChannel? subChannel, int dbNumber)
    {
        if (subChannel == null) return;
        VariableAddress?[] addresses =
        {
            subChannel.AuMode,
            subChannel.TheNm,
            subChannel.Temp,
            subChannel.FlowSet,
            subChannel.ConMode,
            subChannel.DelayTime,
            subChannel.OpenTime,
            subChannel.CrossTime
        };
        foreach (VariableAddress? address in addresses)
        {
            if (address == null) continue;
            if (string.Equals(address.Area, "DB", StringComparison.OrdinalIgnoreCase))
                address.DBNumber = dbNumber;
        }
    }
    private static void ValidateDeviceConfig(PlcDeviceConfig config, string path)
    {
        var errors = new List<string>();
        ValidateAddress(config.InjuctionRun, "InjuctionRun", "BOOL", errors);
        ValidateArray(config.Temperatures, "Temperatures", "REAL", errors, requireItems: true);
        ValidateAddress(config.ActualPressure, "ActualPressure", "REAL", errors);
        ValidateAddress(config.TaiBanActualValue, "TaiBanActualValue", "REAL", errors);
        ValidateAddress(config.TaiBanSetValue, "TaiBanSetValue", "REAL", errors);
        ValidateAddress(config.AmountOfResidualAluminum, "AmountOfResidualAluminum", "REAL", errors);
        ValidateAddress(config.ActualAluminiumTemperature, "ActualAluminiumTemperature", "REAL", errors);
        ValidateAddress(config.SetAluminiumTemperature, "SetAluminiumTemperature", "REAL", errors);
        ValidateAddress(config.ActualFurnaceTemperature, "ActualFurnaceTemperature", "REAL", errors);
        ValidateAddress(config.SetFurnaceTemperature, "SetFurnaceTemperature", "REAL", errors);
        ValidateArray(config.CoolingSetFlow, "CoolingSetFlow", "INT", errors, requireItems: true);
        ValidateArray(config.CoolingActualFlow, "CoolingActualFlow", "INT", errors, requireItems: true);
        if (config.CoolingSetFlow.Length % 2 != 0)
            errors.Add("CoolingSetFlow 数量必须是偶数（前半风冷、后半水冷）。");
        if (config.CoolingActualFlow.Length % 2 != 0)
            errors.Add("CoolingActualFlow 数量必须是偶数（前半风冷、后半水冷）。");
        ThrowIfInvalid(path, errors);
    }
    private static void ValidateProcessConfig(PlcProcessConfig config, string path)
    {
        var errors = new List<string>();
        ValidateArray(config.PressureSet, "PressureSet", "REAL", errors, expectedCount: 9);
        ValidateArray(config.TimeSet, "TimeSet", "DINT", errors, expectedCount: 9);
        ValidateAddress(config.CoolingTimeMax, "CoolingTimeMax", "DINT", errors);
        ValidateAddress(config.CoolingTimeMin, "CoolingTimeMin", "DINT", errors);
        ValidateAddress(config.CastingOpenTemperature, "CastingOpenTemperature", "REAL", errors);
        ValidateAddress(config.CastingOpenMode, "CastingOpenMode", "INT", errors);
        ValidateAddress(config.CastingOpenThermocoupleNm, "CastingOpenThermocoupleNm", "INT", errors);
        if (config.CoolingChannels == null || config.CoolingChannels.Length == 0)
        {
            errors.Add("CoolingChannels 至少需要一个通道。");
        }
        else
        {
            for (int i = 0; i < config.CoolingChannels.Length; i++)
            {
                CoolingChannelConfig? channel = config.CoolingChannels[i];
                string prefix = $"CoolingChannels[{i}]";
                if (channel == null)
                {
                    errors.Add($"{prefix} 不能为空。");
                    continue;
                }
                if (channel.DBNumber <= 0)
                    errors.Add($"{prefix}.DBNumber 必须大于 0。");
                ValidateSubChannel(channel.Air, $"{prefix}.Air", isWater: false, errors);
                ValidateSubChannel(channel.Water, $"{prefix}.Water", isWater: true, errors);
            }
        }
        ThrowIfInvalid(path, errors);
    }
    private static void ValidateSubChannel(
        CoolingSubChannel? channel, string name, bool isWater, List<string> errors)
    {
        if (channel == null)
        {
            errors.Add($"{name} 不能为空。");
            return;
        }
        ValidateAddress(channel.AuMode, $"{name}.AuMode", "INT", errors);
        ValidateAddress(channel.TheNm, $"{name}.TheNm", "INT", errors);
        ValidateAddress(channel.Temp, $"{name}.Temp", "REAL", errors);
        ValidateAddress(channel.FlowSet, $"{name}.FlowSet", isWater ? "REAL" : "INT", errors);
        ValidateAddress(channel.ConMode, $"{name}.ConMode", "INT", errors);
        ValidateAddress(channel.DelayTime, $"{name}.DelayTime", "DINT", errors);
        ValidateAddress(channel.OpenTime, $"{name}.OpenTime", "DINT", errors);
        ValidateAddress(channel.CrossTime, $"{name}.CrossTime", "DINT", errors);
    }
    private static void ValidateArray<TAddress>(
        TAddress[]? addresses, string name, string expectedType, List<string> errors,
        bool requireItems = false, int? expectedCount = null) where TAddress : VariableAddress
    {
        if (addresses == null)
        {
            errors.Add($"{name} 不能为空。");
            return;
        }
        if (requireItems && addresses.Length == 0)
            errors.Add($"{name} 至少需要一个点位。");
        if (expectedCount.HasValue && addresses.Length != expectedCount.Value)
            errors.Add($"{name} 应有 {expectedCount.Value} 个点位，当前为 {addresses.Length} 个。");
        for (int i = 0; i < addresses.Length; i++)
            ValidateAddress(addresses[i], $"{name}[{i}]", expectedType, errors);
    }
    private static void ValidateAddress(
        VariableAddress? address, string name, string expectedType, List<string> errors)
    {
        if (address == null)
        {
            errors.Add($"{name} 地址不能为空。");
            return;
        }
        string area = address.Area?.Trim().ToUpperInvariant() ?? string.Empty;
        string dataType = address.DataType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (area is not ("DB" or "M" or "I" or "Q"))
            errors.Add($"{name}.Area={address.Area} 不受支持，只允许 DB/M/I/Q。");
        if (area == "DB" && address.DBNumber <= 0)
            errors.Add($"{name}.DBNumber 必须大于 0。");
        if (address.ByteOffset < 0)
            errors.Add($"{name}.ByteOffset 不能小于 0。");
        if (!dataType.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
            errors.Add($"{name}.DataType 应为 {expectedType}，当前为 {address.DataType}。");
        if (dataType == "BOOL" && (address.Bit < 0 || address.Bit > 7))
            errors.Add($"{name}.Bit 必须在 0～7 之间。");
    }
    private static void ThrowIfInvalid(string path, List<string> errors)
    {
        if (errors.Count == 0) return;
        throw new InvalidDataException(
            $"PLC 配置校验失败：{path}{Environment.NewLine}- " +
            string.Join(Environment.NewLine + "- ", errors));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PDFOCRTarget.Services;

public static class ConfigService
{
    private static readonly string ConfigDir;
    private static readonly string ConfigFile;
    private static JsonElement _config = JsonDocument.Parse("{}").RootElement;

    static ConfigService()
    {
        var tempDir = Path.GetTempPath();
        ConfigDir = Path.Combine(tempDir, "MCZLFAPP", "PDFOCRTarget");
        ConfigFile = Path.Combine(ConfigDir, "config.json");
    }

    public static void Initialize()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (File.Exists(ConfigFile))
        {
            try
            {
                var json = File.ReadAllText(ConfigFile);
                _config = JsonDocument.Parse(json).RootElement;
            }
            catch
            {
                // 配置文件损坏，使用默认值
            }
        }
    }

    public static T Get<T>(string key, T defaultValue = default!)
    {
        if (_config.TryGetProperty(key, out var value))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(value.GetRawText()) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public static void Set<T>(string key, T value)
    {
        var dict = new Dictionary<string, object?>();
        
        // 复制现有配置
        foreach (var prop in _config.EnumerateObject())
        {
            dict[prop.Name] = JsonSerializer.Deserialize<object?>(prop.Value.GetRawText());
        }
        
        // 更新值
        dict[key] = value;
        
        // 保存
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(dict, options);
        File.WriteAllText(ConfigFile, json);
        
        // 重新加载
        _config = JsonDocument.Parse(json).RootElement;
    }
}

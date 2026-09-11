using System.Text;
using EnhancedQuickPanel.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnhancedQuickPanel;

/// <summary>Reads and writes DefaultConfig.json, including backup and atomic replace.</summary>
internal sealed class ConfigurationStore
{
    internal static ConfigurationStore? Active { get; set; }

    private static readonly JsonSerializerSettings ReadSettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private static readonly JsonSerializerSettings WriteSettings = new()
    {
        Formatting = Formatting.Indented,
        DefaultValueHandling = DefaultValueHandling.Include,
    };

    private readonly string _directory;
    private readonly TimeProvider _time;

    public ConfigurationStore(string directory, TimeProvider? time = null)
    {
        _directory = directory;
        _time = time ?? TimeProvider.System;
    }

    public string FilePath => Path.Combine(_directory, Configuration.FileName);

    public static void SaveActive(Configuration config)
    {
        if (Active is null)
            throw new InvalidOperationException("Configuration store is not initialized.");

        Active.Save(config);
    }

    public Configuration Load()
    {
        if (!File.Exists(FilePath))
        {
            var created = Configuration.CreateNew();
            PanelStylePresets.ApplyDefault(created);
            return created;
        }

        string text;
        try
        {
            text = File.ReadAllText(FilePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw Fail("Failed to read the configuration file.", ex);
        }

        JToken token;
        try
        {
            token = JToken.Parse(text);
        }
        catch (JsonException ex)
        {
            throw Fail("Configuration file is not valid JSON.", ex);
        }

        var inspection = ConfigurationMigration.Inspect(token);
        if (inspection.Kind is ConfigFormatKind.Invalid or ConfigFormatKind.Unsupported)
            throw Fail(inspection.Reason);

        var migrate = inspection.Kind == ConfigFormatKind.Legacy;
        var payload = migrate ? ConfigurationMigration.MigrateToCurrent((JObject)token) : (JObject)token;

        Configuration? config;
        try
        {
            config = JsonConvert.DeserializeObject<Configuration>(payload.ToString(Formatting.None), ReadSettings);
        }
        catch (Exception ex)
        {
            throw Fail("Failed to deserialize the configuration file.", ex);
        }

        if (config is null)
            throw Fail("Configuration file deserialized to null.");

        try
        {
            config.FillMissing();
            config.Validate();
        }
        catch (ConfigurationLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Fail("Failed to complete the loaded configuration.", ex);
        }

        if (migrate)
            PersistMigrated(inspection.SourceVersion, config);

        return config;
    }

    public void Save(Configuration config)
    {
        Directory.CreateDirectory(_directory);
        var path = FilePath;
        var tempPath = path + ".new";
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(config, WriteSettings), Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    private void PersistMigrated(int sourceVersion, Configuration config)
    {
        var backupPath = AllocateBackupPath(sourceVersion);
        try
        {
            File.Copy(FilePath, backupPath, overwrite: false);
        }
        catch (Exception ex)
        {
            throw Fail("Failed to back up the configuration file before migration.", ex);
        }

        try
        {
            Save(config);
        }
        catch (Exception ex)
        {
            throw Fail("Failed to write the migrated configuration file.", ex);
        }
    }

    private string AllocateBackupPath(int sourceVersion)
    {
        var stamp = _time.GetLocalNow().ToString("yyyyMMdd-HHmmss");
        var baseName = $"DefaultConfig.v{sourceVersion}-backup-{stamp}";
        var path = Path.Combine(_directory, baseName + ".json");
        if (!File.Exists(path))
            return path;

        for (var n = 2; n < 1000; n++)
        {
            path = Path.Combine(_directory, $"{baseName}-{n}.json");
            if (!File.Exists(path))
                return path;
        }

        throw Fail("Could not allocate a unique configuration backup name.");
    }

    private ConfigurationLoadException Fail(string message, Exception? inner = null) =>
        new(message, FilePath, inner);
}

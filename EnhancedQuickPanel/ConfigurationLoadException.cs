namespace EnhancedQuickPanel;

/// <summary>Thrown when the plugin configuration cannot be loaded or migrated.</summary>
public sealed class ConfigurationLoadException : Exception
{
    public string? ConfigPath { get; }

    public ConfigurationLoadException(string message, string? configPath = null, Exception? inner = null)
        : base(Format(message, configPath), inner)
    {
        ConfigPath = configPath;
    }

    private static string Format(string message, string? configPath) =>
        string.IsNullOrEmpty(configPath)
            ? message
            : $"{message} Path: {configPath}";
}

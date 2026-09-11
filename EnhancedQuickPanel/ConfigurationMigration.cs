using Newtonsoft.Json.Linq;

namespace EnhancedQuickPanel;

internal enum ConfigFormatKind
{
    Legacy,
    Current,
    Unsupported,
    Invalid,
}

internal readonly record struct ConfigFormatInspection(
    ConfigFormatKind Kind,
    int SourceVersion,
    string Reason);

/// <summary>Inspects ConfigVersion before deserialize and applies in-memory format upgrades.</summary>
internal static class ConfigurationMigration
{
    public static ConfigFormatInspection Inspect(JToken token)
    {
        if (token is not JObject obj)
            return Invalid("Configuration JSON must be an object.");

        if (!obj.TryGetValue("ConfigVersion", out var versionToken)
            || versionToken is null
            || versionToken.Type == JTokenType.Undefined)
        {
            return new ConfigFormatInspection(ConfigFormatKind.Legacy, 0, string.Empty);
        }

        if (versionToken.Type == JTokenType.Null)
            return Invalid("ConfigVersion is null.");

        if (versionToken.Type != JTokenType.Integer)
            return Invalid("ConfigVersion must be an integer.");

        long value;
        try
        {
            value = versionToken.Value<long>();
        }
        catch (Exception)
        {
            return Invalid("ConfigVersion must be an integer.");
        }

        if (value < 0)
            return Invalid("ConfigVersion must not be negative.");

        if (value > int.MaxValue)
        {
            return new ConfigFormatInspection(
                ConfigFormatKind.Unsupported,
                int.MaxValue,
                $"Configuration format v{value} is not supported.");
        }

        var version = (int)value;
        if (version == 0)
            return new ConfigFormatInspection(ConfigFormatKind.Legacy, 0, string.Empty);

        if (version == Configuration.CurrentConfigVersion)
            return new ConfigFormatInspection(ConfigFormatKind.Current, version, string.Empty);

        return new ConfigFormatInspection(
            ConfigFormatKind.Unsupported,
            version,
            $"Configuration format v{version} is not supported.");
    }

    public static JObject MigrateToCurrent(JObject source)
    {
        var copy = (JObject)source.DeepClone();
        copy["ConfigVersion"] = Configuration.CurrentConfigVersion;
        return copy;
    }

    private static ConfigFormatInspection Invalid(string reason) =>
        new(ConfigFormatKind.Invalid, -1, reason);
}

using System.Text.Json;
using Dalamud.Game;

namespace EnhancedQuickPanel;

/// <summary>Loads and looks up localized UI strings for the current client language.</summary>
internal static class I18n
{
    private static readonly Dictionary<string, string> Strings = new(StringComparer.Ordinal);
    private static string _language = "en";

    public static string Language => _language;

    public static void Initialize()
    {
        Reload(ResolveLanguage());
    }

    public static void Reload(string language)
    {
        _language = language;
        Strings.Clear();

        if (!TryLoadLanguage(language))
            TryLoadLanguage("en");
    }

    public static string T(string key) => Get(key);

    public static string T(string key, params object[] args) => Get(key, args);

    public static string Get(string key)
    {
        if (Strings.TryGetValue(key, out var value))
            return value;

        PluginServices.Log.Warning($"[EQP i18n] Missing key '{key}' ({_language})");
        return key;
    }

    public static string Get(string key, params object[] args)
    {
        try
        {
            return string.Format(Get(key), args);
        }
        catch (FormatException ex)
        {
            PluginServices.Log.Warning($"[EQP i18n] Format failed for '{key}': {ex.Message}");
            return Get(key);
        }
    }

    private static string ResolveLanguage() =>
        PluginServices.Data.Language == ClientLanguage.Japanese ? "ja" : "en";

    private static bool TryLoadLanguage(string language)
    {
        try
        {
            var path = Path.Combine(
                PluginServices.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
                "Data",
                "I18n",
                $"{language}.json");
            if (!File.Exists(path))
            {
                PluginServices.Log.Warning($"[EQP i18n] Language file not found: {path}");
                return false;
            }

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (entries == null)
                return false;

            foreach (var (key, value) in entries)
                Strings[key] = value;

            PluginServices.Log.Information($"[EQP i18n] Loaded {Strings.Count} strings ({language})");
            return true;
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning($"[EQP i18n] Failed to load {language}: {ex.Message}");
            return false;
        }
    }
}

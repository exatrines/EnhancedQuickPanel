using System.Reflection;
using System.Text.Json;
using ECommons.DalamudServices;
using ECommons.Logging;

namespace EnhancedQuickPanel.Services;

/// <summary>Loads built-in style presets from JSON and applies them to the configuration.</summary>
internal static class PanelStylePresets
{
    /// <summary>A named style preset and its JSON payload.</summary>
    internal readonly record struct StylePreset(string Id, string DisplayName, string Json);

    /// <summary>One entry in the preset manifest file.</summary>
    private sealed class PresetEntry
    {
        public string Id { get; set; } = string.Empty;

        public string File { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static StylePreset[]? _presets;

    private static StylePreset[] Presets
    {
        get
        {
            if (_presets == null || _presets.Length == 0)
                _presets = LoadPresets();

            return _presets;
        }
    }

    public static ReadOnlySpan<StylePreset> All => Presets;

    public static string GetLabel(in StylePreset preset) => preset.DisplayName;

    public static void ApplyDefault(Configuration config)
    {
        if (Presets.Length == 0)
            return;

        _ = TryApply(config, 0, out _);
    }

    public static void ApplyFromUi(int index)
    {
        if (!TryApply(C, index, out var error))
        {
            Notify.Warning(error);
            return;
        }

        C.EnsureDefaults();
        EzConfig.Save();
        Notify.Success(T("panelStyle.presetApplied", GetLabel(Presets[index])));
    }

    public static bool TryApply(Configuration config, int index, out string error)
    {
        error = string.Empty;
        if (index < 0 || index >= Presets.Length)
        {
            error = T("panelStyle.error.unknownPreset");
            return false;
        }

        return PanelStyleImportExport.TryImport(Presets[index].Json, config, out error);
    }

    private static string ResolvePluginDirectory()
    {
        try
        {
            var svcDir = Svc.PluginInterface.AssemblyLocation.DirectoryName;
            if (!string.IsNullOrEmpty(svcDir))
                return svcDir;
        }
        catch
        {
        }

        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
    }

    private static StylePreset[] LoadPresets()
    {
        var presetDir = Path.Combine(ResolvePluginDirectory(), "StylePresets");
        var manifestPath = Path.Combine(presetDir, "presets.json");

        if (!File.Exists(manifestPath))
        {
            PluginLog.Warning($"[EQP] Style preset manifest not found: {manifestPath}");
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<PresetEntry[]>(File.ReadAllText(manifestPath), ManifestJsonOptions);
            if (entries == null || entries.Length == 0)
                return [];

            var presets = new List<StylePreset>(entries.Length);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.File))
                    continue;

                var path = Path.Combine(presetDir, entry.File);
                if (!File.Exists(path))
                {
                    PluginLog.Warning($"[EQP] Style preset file not found: {path}");
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? Path.GetFileNameWithoutExtension(entry.File)
                    : entry.DisplayName;
                var id = string.IsNullOrWhiteSpace(entry.Id)
                    ? displayName.ToLowerInvariant()
                    : entry.Id;

                presets.Add(new StylePreset(id, displayName, File.ReadAllText(path)));
            }

            PluginLog.Information($"[EQP] Loaded {presets.Count} style presets.");
            return presets.ToArray();
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"[EQP] Failed to load style presets: {ex.Message}");
            return [];
        }
    }
}

using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services.CustomIcons;

namespace EnhancedQuickPanel.Services;

internal enum PluginShortcutVisual
{
    None,
    Enabled,
    Disabled,
    Processing,
    Missing,
}

/// <summary>Resolves, draws, and executes plugin slots. Enable/disable is PluginLifecycleToggle.</summary>
internal static class PluginShortcuts
{
    private static bool iconApiUnavailable;

    public static bool IsSelf(string name) =>
        string.Equals(name, PluginServices.PluginInterface.InternalName, StringComparison.Ordinal);

    public static IExposedPlugin? Find(string name) => PluginServices.PluginInterface.InstalledPlugins
        .FirstOrDefault(plugin => plugin.InternalName == name);

    public static PluginShortcutVisual ResolveVisual(PanelSlot slot)
    {
        if (slot.Kind != PanelSlotKind.Plugin || string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return PluginShortcutVisual.None;
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName))
            return PluginShortcutVisual.Processing;
        var plugin = Find(slot.PluginInternalName);
        if (plugin == null)
            return PluginShortcutVisual.Missing;
        return plugin.IsLoaded ? PluginShortcutVisual.Enabled : PluginShortcutVisual.Disabled;
    }

    public static bool IsDimmed(PluginShortcutVisual visual) =>
        visual is PluginShortcutVisual.Disabled or PluginShortcutVisual.Processing or PluginShortcutVisual.Missing;

    public static void Execute(PanelSlot slot, ImGuiMouseButton button)
    {
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName))
            return;

        switch (button)
        {
            case ImGuiMouseButton.Right:
                OpenUi(slot, preferMain: false);
                return;
            case ImGuiMouseButton.Middle:
                if (Config.PluginMiddleClickTogglesEnabled)
                    PluginLifecycleToggle.Request(slot.PluginInternalName);
                return;
            default:
                OpenUi(slot, preferMain: true);
                return;
        }
    }

    public static bool AppearsInPicker(IExposedPlugin plugin, string selectedInternalName)
    {
        if (string.Equals(plugin.InternalName, selectedInternalName, StringComparison.Ordinal))
            return true;
        if (!plugin.IsLoaded)
            return true;
        return plugin.HasMainUi || plugin.HasConfigUi;
    }

    public static bool TryGetIcon(PanelSlot slot, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (slot.Kind != PanelSlotKind.Plugin || slot.IconId != 0)
            return false;
        if (string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return false;

        if (TryGetDalamudIcon(slot.PluginInternalName, out texture))
            return true;
        if (PluginIconStore.TryGetTexture(slot.PluginInternalName, out texture))
            return true;
        if (!PluginIconStore.NeedsDownload(slot.PluginInternalName))
            return false;

        var plugin = Find(slot.PluginInternalName);
        if (plugin != null)
            PluginIconStore.RequestDownload(slot.PluginInternalName, ReadIconUrl(plugin));
        return false;
    }

    private static bool TryGetDalamudIcon(string internalName, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (iconApiUnavailable)
            return false;
        try
        {
            var plugin = Find(internalName);
            if (plugin == null)
                return false;
            var cache = DalamudReflection.GetService("Dalamud.Interface.Internal.Windows.PluginImageCache");
            var method = cache.GetType().GetMethod("TryGetIcon", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException("PluginImageCache.TryGetIcon");
            object?[] args = [FindLocal(internalName), plugin.Manifest, plugin.IsThirdParty, null, null];
            method.Invoke(cache, args);
            texture = args[3] as IDalamudTextureWrap ?? null!;
            return texture != null;
        }
        catch (Exception ex)
        {
            iconApiUnavailable = true;
            PluginServices.Log.Warning($"Plugin icons unavailable; cached and custom icons remain usable: {ex.Message}");
            return false;
        }
    }

    private static string? ReadIconUrl(IExposedPlugin plugin)
    {
        var manifest = plugin.Manifest;
        return manifest.GetType().GetProperty("IconUrl")?.GetValue(manifest) as string;
    }

    internal static object? FindLocal(string internalName)
    {
        var manager = DalamudReflection.GetPluginManager();
        if (manager.GetType().GetProperty("InstalledPlugins")?.GetValue(manager) is not System.Collections.IEnumerable installed)
            return null;
        return installed.Cast<object>().FirstOrDefault(plugin =>
            plugin.GetType().GetProperty("InternalName")?.GetValue(plugin) as string == internalName);
    }

    internal static Type LocalType(object local)
    {
        var type = local.GetType();
        return type.Name == "LocalDevPlugin" ? type.BaseType ?? type : type;
    }

    public static void OpenUi(PanelSlot slot, bool preferMain)
    {
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName))
            return;

        try
        {
            var plugin = Find(slot.PluginInternalName);
            if (plugin == null)
            {
                PluginServices.Chat.PrintError(T("shortcut.unavailable"));
                return;
            }
            if (!plugin.IsLoaded)
            {
                PluginServices.Chat.PrintError(T("shortcut.notLoaded"));
                return;
            }

            if (preferMain)
            {
                if (plugin.HasMainUi)
                    plugin.OpenMainUi();
                else if (plugin.HasConfigUi)
                    plugin.OpenConfigUi();
                return;
            }

            if (plugin.HasConfigUi)
                plugin.OpenConfigUi();
            else if (plugin.HasMainUi)
                plugin.OpenMainUi();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error($"Could not open plugin {slot.PluginInternalName}: {ex}");
            PluginServices.Chat.PrintError(T("shortcut.unavailable"));
        }
    }
}

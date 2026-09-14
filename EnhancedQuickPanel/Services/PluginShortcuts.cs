using System.Collections;
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

    public static IExposedPlugin? Find(PanelSlot slot) =>
        FindEntry(slot.PluginInternalName, slot.PluginWorkingPluginId)?.Plugin;

    public static PluginShortcutVisual ResolveVisual(PanelSlot slot)
    {
        if (slot.Kind != PanelSlotKind.Plugin || string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return PluginShortcutVisual.None;
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName, slot.PluginWorkingPluginId))
            return PluginShortcutVisual.Processing;
        var plugin = Find(slot);
        if (plugin == null)
            return PluginShortcutVisual.Missing;
        return plugin.IsLoaded ? PluginShortcutVisual.Enabled : PluginShortcutVisual.Disabled;
    }

    public static bool IsDimmed(PluginShortcutVisual visual) =>
        visual is PluginShortcutVisual.Disabled or PluginShortcutVisual.Processing or PluginShortcutVisual.Missing;

    public static void Execute(PanelSlot slot, ImGuiMouseButton button)
    {
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName, slot.PluginWorkingPluginId))
            return;

        switch (button)
        {
            case ImGuiMouseButton.Right:
                OpenUi(slot, preferMain: false);
                return;
            case ImGuiMouseButton.Middle:
                if (Config.PluginMiddleClickTogglesEnabled)
                    ToggleEnabled(slot);
                return;
            default:
                OpenUi(slot, preferMain: true);
                return;
        }
    }

    public static void ToggleEnabled(PanelSlot slot)
    {
        BindResolvedWorkingId(slot);
        PluginLifecycleToggle.Request(slot.PluginInternalName, slot.PluginWorkingPluginId);
    }

    public static void OpenUi(PanelSlot slot, bool preferMain)
    {
        if (PluginLifecycleToggle.IsBusy(slot.PluginInternalName, slot.PluginWorkingPluginId))
            return;

        try
        {
            BindResolvedWorkingId(slot);
            var plugin = Find(slot);
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

    public static bool AppearsInPicker(InstalledPluginEntry entry, PanelSlot slot)
    {
        if (IsSelected(entry, slot))
            return true;
        if (!entry.Plugin.IsLoaded)
            return true;
        return entry.Plugin.HasMainUi || entry.Plugin.HasConfigUi;
    }

    public static bool IsSelected(InstalledPluginEntry entry, PanelSlot slot)
    {
        var resolved = FindEntry(slot.PluginInternalName, slot.PluginWorkingPluginId);
        if (resolved == null)
            return false;
        if (SameWorkingId(entry.WorkingId, resolved.Value.WorkingId))
            return true;
        return !HasWorkingId(resolved.Value.WorkingId)
            && ReferenceEquals(entry.Plugin, resolved.Value.Plugin);
    }

    public static string PickerLabel(InstalledPluginEntry entry, IReadOnlyList<InstalledPluginEntry> all)
    {
        var plugin = entry.Plugin;
        var nameCount = 0;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Plugin.Name == plugin.Name)
                nameCount++;
        }

        if (nameCount <= 1)
            return plugin.Name;

        var label = plugin.IsLoaded ? plugin.Name : T("slot.tooltip.pluginDisabled", plugin.Name);
        if (plugin.IsDev)
            label += " (Dev)";
        var sameState = 0;
        for (var i = 0; i < all.Count; i++)
        {
            var other = all[i].Plugin;
            if (other.Name == plugin.Name && other.IsLoaded == plugin.IsLoaded && other.IsDev == plugin.IsDev)
                sameState++;
        }

        if (sameState > 1)
            label += $" {plugin.Version}";
        return label;
    }

    public static string ReadAuthor(IExposedPlugin plugin)
    {
        var manifest = plugin.Manifest;
        return manifest.GetType().GetProperty("Author")?.GetValue(manifest) as string ?? string.Empty;
    }

    internal static void Assign(PanelSlot slot, InstalledPluginEntry entry)
    {
        var nameChanged = entry.Plugin.InternalName != slot.PluginInternalName;
        slot.PluginInternalName = entry.Plugin.InternalName;
        slot.PluginWorkingPluginId = entry.WorkingId;
        if (nameChanged)
            slot.IconId = 0;
        Config.Save();
    }

    internal static IReadOnlyList<InstalledPluginEntry> ListPickerEntries()
    {
        var list = ListInstalled().ToList();
        list.Sort(static (a, b) =>
        {
            var byName = string.Compare(a.Plugin.Name, b.Plugin.Name, StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
                return byName;
            var byInternal = string.Compare(a.Plugin.InternalName, b.Plugin.InternalName, StringComparison.Ordinal);
            if (byInternal != 0)
                return byInternal;
            var byLoaded = b.Plugin.IsLoaded.CompareTo(a.Plugin.IsLoaded);
            if (byLoaded != 0)
                return byLoaded;
            return string.Compare(a.WorkingId, b.WorkingId, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    private static void BindResolvedWorkingId(PanelSlot slot)
    {
        if (HasWorkingId(slot.PluginWorkingPluginId))
            return;
        var entry = FindEntry(slot.PluginInternalName, string.Empty);
        if (entry == null || !HasWorkingId(entry.Value.WorkingId))
            return;
        slot.PluginWorkingPluginId = entry.Value.WorkingId;
        Config.Save();
    }

    public static bool TryGetIcon(PanelSlot slot, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (slot.Kind != PanelSlotKind.Plugin || slot.IconId != 0)
            return false;
        if (string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return false;
        var plugin = Find(slot);
        return TryGetIcon(plugin, slot.PluginInternalName, slot.PluginWorkingPluginId, out texture);
    }

    public static bool TryGetIcon(InstalledPluginEntry entry, out IDalamudTextureWrap texture) =>
        TryGetIcon(entry.Plugin, entry.Plugin.InternalName, entry.WorkingId, out texture);

    private static bool TryGetIcon(
        IExposedPlugin? plugin,
        string internalName,
        string workingPluginId,
        out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (string.IsNullOrWhiteSpace(internalName))
            return false;
        if (TryGetDalamudIcon(plugin, internalName, workingPluginId, out texture))
            return true;
        if (PluginIconStore.TryGetTexture(internalName, out texture))
            return true;
        if (!PluginIconStore.NeedsDownload(internalName) || plugin == null)
            return false;
        PluginIconStore.RequestDownload(internalName, ReadIconUrl(plugin));
        return false;
    }

    private static bool TryGetDalamudIcon(
        IExposedPlugin? plugin,
        string internalName,
        string workingPluginId,
        out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (iconApiUnavailable || plugin == null)
            return false;
        try
        {
            var cache = DalamudReflection.GetService("Dalamud.Interface.Internal.Windows.PluginImageCache");
            var method = cache.GetType().GetMethod("TryGetIcon", BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException("PluginImageCache.TryGetIcon");
            object?[] args = [FindLocal(internalName, workingPluginId), plugin.Manifest, plugin.IsThirdParty, null, null];
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

    internal static object? FindLocal(string internalName, string workingPluginId = "")
    {
        if (HasWorkingId(workingPluginId))
        {
            foreach (var local in EnumerateLocals())
            {
                if (SameWorkingId(ReadWorkingId(local), workingPluginId))
                    return local;
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(internalName))
            return null;

        object? first = null;
        object? loaded = null;
        foreach (var local in EnumerateLocals())
        {
            if (!string.Equals(ReadLocalInternalName(local), internalName, StringComparison.Ordinal))
                continue;
            first ??= local;
            if (loaded == null && IsLocalLoaded(local))
                loaded = local;
        }

        return loaded ?? first;
    }

    internal static bool HasOtherLoadedInstance(object local)
    {
        var name = ReadLocalInternalName(local);
        var id = ReadWorkingId(local);
        if (string.IsNullOrEmpty(name))
            return false;
        foreach (var other in EnumerateLocals())
        {
            if (!string.Equals(ReadLocalInternalName(other), name, StringComparison.Ordinal))
                continue;
            if (HasWorkingId(id) && SameWorkingId(ReadWorkingId(other), id))
                continue;
            if (IsLocalLoaded(other))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<InstalledPluginEntry> ListInstalled()
    {
        var exposed = PluginServices.PluginInterface.InstalledPlugins.ToList();
        var locals = EnumerateLocals().ToList();
        var used = new bool[locals.Count];
        var localIndexByExposed = new int[exposed.Count];
        Array.Fill(localIndexByExposed, -1);

        void Pair(int exposedIndex, bool requireLoaded)
        {
            if (localIndexByExposed[exposedIndex] >= 0)
                return;
            var plugin = exposed[exposedIndex];
            if (requireLoaded && !plugin.IsLoaded)
                return;
            for (var j = 0; j < locals.Count; j++)
            {
                if (used[j])
                    continue;
                if (!string.Equals(ReadLocalInternalName(locals[j]), plugin.InternalName, StringComparison.Ordinal))
                    continue;
                if (requireLoaded && !IsLocalLoaded(locals[j]))
                    continue;
                used[j] = true;
                localIndexByExposed[exposedIndex] = j;
                return;
            }
        }

        for (var i = 0; i < exposed.Count; i++)
            Pair(i, requireLoaded: true);
        for (var i = 0; i < exposed.Count; i++)
            Pair(i, requireLoaded: false);

        var result = new List<InstalledPluginEntry>(exposed.Count);
        for (var i = 0; i < exposed.Count; i++)
        {
            var localIndex = localIndexByExposed[i];
            object? local = localIndex >= 0 ? locals[localIndex] : null;
            result.Add(new InstalledPluginEntry(
                exposed[i],
                local,
                local == null ? string.Empty : ReadWorkingId(local)));
        }

        return result;
    }

    internal static Type LocalType(object local)
    {
        var type = local.GetType();
        return type.Name == "LocalDevPlugin" ? type.BaseType ?? type : type;
    }

    internal static bool IsLocalLoaded(object local) =>
        LocalType(local).GetProperty("IsLoaded")?.GetValue(local) as bool? ?? false;

    internal static string ReadLocalInternalName(object local)
    {
        var type = LocalType(local);
        if (type.GetProperty("InternalName")?.GetValue(local) is string name && !string.IsNullOrEmpty(name))
            return name;
        var manifest = type.GetProperty("Manifest")?.GetValue(local);
        return manifest?.GetType().GetProperty("InternalName")?.GetValue(manifest) as string ?? string.Empty;
    }

    private static InstalledPluginEntry? FindEntry(string internalName, string workingPluginId)
    {
        var installed = ListInstalled();
        if (HasWorkingId(workingPluginId))
        {
            for (var i = 0; i < installed.Count; i++)
            {
                if (SameWorkingId(installed[i].WorkingId, workingPluginId))
                    return installed[i];
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(internalName))
            return null;

        InstalledPluginEntry? first = null;
        InstalledPluginEntry? loaded = null;
        for (var i = 0; i < installed.Count; i++)
        {
            var entry = installed[i];
            if (!string.Equals(entry.Plugin.InternalName, internalName, StringComparison.Ordinal))
                continue;
            first ??= entry;
            if (loaded == null && entry.Plugin.IsLoaded)
                loaded = entry;
        }

        return loaded ?? first;
    }

    private static IEnumerable<object> EnumerateLocals()
    {
        var manager = DalamudReflection.GetPluginManager();
        if (manager.GetType().GetProperty("InstalledPlugins")?.GetValue(manager) is not IEnumerable installed)
            yield break;
        foreach (var plugin in installed)
        {
            if (plugin != null)
                yield return plugin;
        }
    }

    private static string ReadWorkingId(object local)
    {
        var value = LocalType(local).GetProperty("EffectiveWorkingPluginId")?.GetValue(local);
        return value is Guid guid && guid != Guid.Empty ? guid.ToString("D") : string.Empty;
    }

    private static bool HasWorkingId(string workingPluginId) =>
        !string.IsNullOrWhiteSpace(workingPluginId);

    private static bool SameWorkingId(string left, string right) =>
        HasWorkingId(left)
        && HasWorkingId(right)
        && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

internal readonly record struct InstalledPluginEntry(
    IExposedPlugin Plugin,
    object? Local,
    string WorkingId);

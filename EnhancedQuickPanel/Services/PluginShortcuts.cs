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
    private static int cachedFrame = -1;
    private static IReadOnlyList<object> cachedLocals = [];
    private static IReadOnlyList<InstalledPluginEntry> cachedInstalled = [];
    private static IReadOnlyList<InstalledPluginEntry> cachedPicker = [];
    private static object? pluginManager;
    private static PropertyInfo? installedPluginsProperty;

    internal static void Invalidate()
    {
        cachedFrame = -1;
        PluginIconStore.Invalidate();
    }

    public static bool IsSelf(string name) =>
        string.Equals(name, PluginServices.PluginInterface.InternalName, StringComparison.Ordinal);

    public static IExposedPlugin? Find(PanelSlot slot) =>
        ResolveSlot(slot)?.Plugin;

    public static bool IsDev(PanelSlot slot) =>
        slot.Kind == PanelSlotKind.Plugin
        && !string.IsNullOrWhiteSpace(slot.PluginInternalName)
        && Find(slot)?.IsDev == true;

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

    public static bool IsIconDownloading(string internalName) =>
        PluginIconStore.IsDownloading(internalName);

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
        var resolved = ResolveSlot(slot);
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
        EnsureFrameCache();
        return cachedPicker;
    }

    private static void BindResolvedWorkingId(PanelSlot slot) =>
        ResolveSlot(slot);

    public static bool TryGetIcon(PanelSlot slot, out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (slot.Kind != PanelSlotKind.Plugin || slot.IconId != 0)
            return false;
        if (string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return false;
        var entry = ResolveSlot(slot);
        return PluginIconStore.TryGet(entry?.Plugin, slot.PluginInternalName, out texture);
    }

    public static bool TryGetIcon(InstalledPluginEntry entry, out IDalamudTextureWrap texture) =>
        PluginIconStore.TryGet(entry.Plugin, entry.Plugin.InternalName, out texture);

    public static void RefreshIcon(PanelSlot slot)
    {
        if (slot.Kind != PanelSlotKind.Plugin || string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return;

        var entry = ResolveSlot(slot);
        PluginIconStore.Refresh(entry?.Plugin, slot.PluginInternalName);
    }

    private static InstalledPluginEntry? ResolveSlot(PanelSlot slot)
    {
        var entry = FindEntry(slot.PluginInternalName, slot.PluginWorkingPluginId);
        if (entry != null)
            TryHealWorkingId(slot, entry.Value);
        return entry;
    }

    private static void TryHealWorkingId(PanelSlot slot, InstalledPluginEntry entry)
    {
        if (!HasWorkingId(entry.WorkingId))
            return;
        if (SameWorkingId(slot.PluginWorkingPluginId, entry.WorkingId))
            return;
        slot.PluginWorkingPluginId = entry.WorkingId;
        Config.Save();
    }

    internal static object? FindLocal(string internalName, string workingPluginId = "")
    {
        EnsureFrameCache();
        var index = ResolveUniqueIndex(
            internalName,
            workingPluginId,
            cachedLocals.Count,
            i => ReadWorkingId(cachedLocals[i]),
            i => ReadLocalInternalName(cachedLocals[i]));
        return index >= 0 ? cachedLocals[index] : null;
    }

    internal static bool HasOtherLoadedInstance(object local)
    {
        var name = ReadLocalInternalName(local);
        var id = ReadWorkingId(local);
        if (string.IsNullOrEmpty(name))
            return false;
        EnsureFrameCache();
        foreach (var other in cachedLocals)
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
        EnsureFrameCache();
        return cachedInstalled;
    }

    private static void EnsureFrameCache()
    {
        var frame = ImGui.GetFrameCount();
        if (cachedFrame == frame)
            return;

        var locals = CollectLocals();
        var installed = PairInstalled(locals);
        cachedLocals = locals;
        cachedInstalled = installed;
        cachedPicker = SortPicker(installed);
        cachedFrame = frame;
    }

    private static IReadOnlyList<object> CollectLocals()
    {
        pluginManager ??= DalamudReflection.GetPluginManager();
        installedPluginsProperty ??= pluginManager.GetType().GetProperty("InstalledPlugins");
        if (installedPluginsProperty?.GetValue(pluginManager) is not IEnumerable installed)
            return [];

        var locals = new List<object>();
        foreach (var plugin in installed)
        {
            if (plugin != null)
                locals.Add(plugin);
        }

        return locals;
    }

    private static IReadOnlyList<InstalledPluginEntry> PairInstalled(IReadOnlyList<object> locals)
    {
        var exposed = PluginServices.PluginInterface.InstalledPlugins.ToList();
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

    private static IReadOnlyList<InstalledPluginEntry> SortPicker(IReadOnlyList<InstalledPluginEntry> installed)
    {
        var list = installed.ToList();
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
        var index = ResolveUniqueIndex(
            internalName,
            workingPluginId,
            installed.Count,
            i => installed[i].WorkingId,
            i => installed[i].Plugin.InternalName);
        return index >= 0 ? installed[index] : null;
    }

    private static int ResolveUniqueIndex(
        string internalName,
        string workingPluginId,
        int count,
        Func<int, string> workingIdAt,
        Func<int, string> internalNameAt)
    {
        if (HasWorkingId(workingPluginId))
        {
            for (var i = 0; i < count; i++)
            {
                if (SameWorkingId(workingIdAt(i), workingPluginId))
                    return i;
            }
        }

        if (string.IsNullOrWhiteSpace(internalName))
            return -1;

        var found = -1;
        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(internalNameAt(i), internalName, StringComparison.Ordinal))
                continue;
            if (found >= 0)
                return -1;
            found = i;
        }

        return found;
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

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel;

/// <summary>Addon lookup and guarded UI calls.</summary>
internal static unsafe class AddonAccess
{
    public static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error($"{ex.Message}\n{ex.StackTrace ?? string.Empty}");
        }
    }

    public static bool IsAddonReady(AtkUnitBase* addon) =>
        addon != null
        && addon->IsVisible
        && addon->UldManager.LoadedState == AtkLoadState.Loaded
        && addon->IsFullyLoaded();

    public static bool TryGetAddonByName<T>(string name, out T* addon) where T : unmanaged
    {
        addon = (T*)PluginServices.GameGui.GetAddonByName(name, 1).Address;
        return addon != null;
    }
}

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Access helpers for the native quick panel addon (show/hide).</summary>
internal static unsafe class NativeQuickPanelAddon
{
    public const string AddonName = "QuickPanel";

    public static bool IsVisible =>
        TryGetAddon(out var addon) && AddonAccess.IsAddonReady(addon);

    public static void HideNative()
    {
        if (!TryGetAddon(out var addon) || !AddonAccess.IsAddonReady(addon))
            return;

        addon->Hide(false, true, 0);
    }

    public static void SuppressNativeIfVisible()
    {
        if (IsVisible)
            HideNative();
    }

    public static bool TryGetAddon(out AtkUnitBase* addon)
    {
        addon = null;
        if (!AddonAccess.TryGetAddonByName<AtkUnitBase>(AddonName, out addon))
            return false;

        return addon != null;
    }
}

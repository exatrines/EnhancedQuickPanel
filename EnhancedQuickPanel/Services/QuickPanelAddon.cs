using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Access helpers for the native quick panel addon (show/hide and bounds).</summary>
internal static unsafe class QuickPanelAddon
{
    public const string AddonName = "QuickPanel";

    public static bool IsVisible =>
        TryGetAddon(out var addon) && GenericHelpers.IsAddonReady(addon);

    public static int ActivePage
    {
        get
        {
            var agent = AgentQuickPanel.Instance();
            if (agent == null)
                return 0;

            var page = (int)agent->ActivePanel;
            return Math.Clamp(page, 0, Configuration.NativePageCount - 1);
        }
    }

    public static void OpenPage(int page)
    {
        var agent = AgentQuickPanel.Instance();
        if (agent == null)
            return;

        agent->OpenPanel((uint)Math.Clamp(page, 0, Configuration.NativePageCount - 1), false, false);
    }

    public static void HideNative()
    {
        if (!TryGetAddon(out var addon) || !GenericHelpers.IsAddonReady(addon))
            return;

        addon->Hide(false, true, 0);
    }

    public static void SuppressNativeIfVisible()
    {
        if (IsVisible)
            HideNative();
    }

    public static void TryCloseNative() => HideNative();

    public static bool TryGetScreenBounds(out QuickPanelBounds bounds)
    {
        bounds = default;
        if (!TryGetAddon(out var addon) || !GenericHelpers.IsAddonReady(addon))
            return false;

        var root = addon->RootNode;
        if (root == null)
            return false;

        var scale = root->ScaleX > 0f ? root->ScaleX : 1f;
        var width = root->Width * scale;
        var height = root->Height * scale;
        if (width <= 0f || height <= 0f)
            return false;

        bounds = new QuickPanelBounds(addon->X, addon->Y, width, height);
        return true;
    }

    public static bool TryGetAddon(out AtkUnitBase* addon)
    {
        addon = null;
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out addon))
            return false;

        return addon != null;
    }
}

/// <summary>Screen bounds of the native quick panel.</summary>
internal readonly record struct QuickPanelBounds(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;
}


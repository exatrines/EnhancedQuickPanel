using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EnhancedQuickPanel.Services;

/// <summary>Coordinates showing the plugin overlay versus the native quick panel.</summary>
internal static unsafe class QuickPanelDisplayCoordinator
{
    public static void HandleOpenPanel(
        AgentQuickPanel.Delegates.OpenPanel original,
        AgentQuickPanel* agent,
        uint panel,
        bool closeIfAlreadyOpen,
        bool showFirstTimeHelp)
    {
        switch (C.DisplayMode)
        {
            // Native mode: let the game open the native quick panel as usual.
            // The plugin overlay is controlled separately (via /eqp), so leave it untouched.
            case QuickPanelDisplayMode.NativeOnly:
                original(agent, panel, closeIfAlreadyOpen, showFirstTimeHelp);
                break;

            // Plugin mode: suppress the native quick panel and toggle the overlay.
            case QuickPanelDisplayMode.PluginOnly:
                ToggleOverlayRequest?.Invoke();
                break;
        }
    }

    public static void ApplyFrameRules()
    {
        // The overlay's visibility is independent of the display mode; only keep the native
        // quick panel hidden while in plugin mode.
        if (C.DisplayMode == QuickPanelDisplayMode.PluginOnly)
            QuickPanelAddon.SuppressNativeIfVisible();
    }

    public static void SetDisplayMode(QuickPanelDisplayMode mode)
    {
        if (C.DisplayMode == mode)
            return;

        C.DisplayMode = mode;

        // Switching to plugin mode immediately hides any currently visible native panel.
        // The overlay itself is not touched here; it stays under /eqp control.
        if (mode == QuickPanelDisplayMode.PluginOnly)
            QuickPanelAddon.HideNative();

        EzConfig.Save();
    }
}


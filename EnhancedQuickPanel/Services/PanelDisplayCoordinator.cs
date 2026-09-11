using EnhancedQuickPanel.Models;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EnhancedQuickPanel.Services;

/// <summary>Coordinates showing the plugin overlay versus the native quick panel.</summary>
internal static unsafe class PanelDisplayCoordinator
{
    public static void HandleOpenPanel(
        AgentQuickPanel.Delegates.OpenPanel original,
        AgentQuickPanel* agent,
        uint panel,
        bool closeIfAlreadyOpen,
        bool showFirstTimeHelp)
    {
        switch (Config.DisplayMode)
        {
            // Native mode: let the game open the native quick panel as usual.
            // The plugin overlay is controlled separately (via /eqp), so leave it untouched.
            case PanelDisplayMode.NativeOnly:
                original(agent, panel, closeIfAlreadyOpen, showFirstTimeHelp);
                break;

            // Plugin mode: suppress the native quick panel and toggle the overlay.
            case PanelDisplayMode.PluginOnly:
                ToggleOverlay();
                break;
        }
    }

    public static void ApplyFrameRules()
    {
        // The overlay's visibility is independent of the display mode; only keep the native
        // quick panel hidden while in plugin mode.
        if (Config.DisplayMode == PanelDisplayMode.PluginOnly)
            NativeQuickPanelAddon.SuppressNativeIfVisible();
    }

    public static void SetDisplayMode(PanelDisplayMode mode)
    {
        if (Config.DisplayMode == mode)
            return;

        Config.DisplayMode = mode;

        // Switching to plugin mode immediately hides any currently visible native panel.
        // The overlay itself is not touched here; it stays under /eqp control.
        if (mode == PanelDisplayMode.PluginOnly)
            NativeQuickPanelAddon.HideNative();

        Config.Save();
    }
}


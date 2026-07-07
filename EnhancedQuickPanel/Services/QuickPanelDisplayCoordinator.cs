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
            case QuickPanelDisplayMode.NativeOnly:
                original(agent, panel, closeIfAlreadyOpen, showFirstTimeHelp);
                C.Enabled = false;
                break;

            case QuickPanelDisplayMode.PluginOnly:
                C.Enabled = !C.Enabled;
                QuickPanelAddon.HideNative();
                break;
        }

        EzConfig.Save();
    }

    public static void ApplyFrameRules()
    {
        switch (C.DisplayMode)
        {
            case QuickPanelDisplayMode.NativeOnly:
                if (C.Enabled)
                    C.Enabled = false;
                break;

            case QuickPanelDisplayMode.PluginOnly:
                QuickPanelAddon.SuppressNativeIfVisible();
                break;
        }
    }

    public static void SetDisplayMode(QuickPanelDisplayMode mode)
    {
        if (C.DisplayMode == mode)
            return;

        C.DisplayMode = mode;

        switch (mode)
        {
            case QuickPanelDisplayMode.NativeOnly:
                C.Enabled = false;
                break;

            case QuickPanelDisplayMode.PluginOnly:
                QuickPanelAddon.HideNative();
                break;
        }

        EzConfig.Save();
    }
}


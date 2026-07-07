using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EnhancedQuickPanel.Services;

/// <summary>Hooks the native quick panel addon to control its visibility.</summary>
internal sealed unsafe class QuickPanelNativeInterceptor : IDisposable
{
    private Hook<AgentQuickPanel.Delegates.OpenPanel>? _openPanelHook;
    private bool _installAttempted;

    public QuickPanelNativeInterceptor()
    {
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        QuickPanelDisplayCoordinator.ApplyFrameRules();

        if (_installAttempted)
            return;

        _installAttempted = true;

        var address = (nint)AgentQuickPanel.MemberFunctionPointers.OpenPanel;
        if (address == 0)
            return;

        _openPanelHook = Svc.Hook.HookFromAddress<AgentQuickPanel.Delegates.OpenPanel>(
            address,
            OpenPanelDetour);
        _openPanelHook.Enable();
    }

    private void OpenPanelDetour(
        AgentQuickPanel* agent,
        uint panel,
        bool closeIfAlreadyOpen,
        bool showFirstTimeHelp)
    {
        if (_openPanelHook == null)
            return;

        QuickPanelDisplayCoordinator.HandleOpenPanel(
            _openPanelHook.Original,
            agent,
            panel,
            closeIfAlreadyOpen,
            showFirstTimeHelp);
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        _openPanelHook?.Dispose();
    }
}


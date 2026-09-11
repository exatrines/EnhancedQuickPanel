using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace EnhancedQuickPanel.Services;

/// <summary>Hooks the native quick panel addon to control its visibility.</summary>
internal sealed unsafe class NativeQuickPanelInterceptor : IDisposable
{
    private Hook<AgentQuickPanel.Delegates.OpenPanel>? _openPanelHook;
    private bool _installAttempted;
    private string? _lastError;
    private int _suppressedErrors;

    public NativeQuickPanelInterceptor()
    {
        PluginServices.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        try
        {
            PanelDisplayCoordinator.ApplyFrameRules();
            TryInstallHook();
            NoteSuccess();
        }
        catch (Exception ex)
        {
            NoteFailure(ex);
        }
    }

    private void OpenPanelDetour(
        AgentQuickPanel* agent,
        uint panel,
        bool closeIfAlreadyOpen,
        bool showFirstTimeHelp)
    {
        try
        {
            if (_openPanelHook == null)
                return;

            PanelDisplayCoordinator.HandleOpenPanel(
                _openPanelHook.Original,
                agent,
                panel,
                closeIfAlreadyOpen,
                showFirstTimeHelp);
            NoteSuccess();
        }
        catch (Exception ex)
        {
            NoteFailure(ex);
        }
    }

    public void Dispose()
    {
        PluginServices.Framework.Update -= OnFrameworkUpdate;
        _openPanelHook?.Dispose();
    }

    private void TryInstallHook()
    {
        if (_installAttempted)
            return;

        _installAttempted = true;
        var address = (nint)AgentQuickPanel.MemberFunctionPointers.OpenPanel;
        if (address == 0)
            return;

        _openPanelHook = PluginServices.Hook.HookFromAddress<AgentQuickPanel.Delegates.OpenPanel>(
            address,
            OpenPanelDetour);
        _openPanelHook.Enable();
    }

    private void NoteSuccess()
    {
        if (_suppressedErrors > 0)
            PluginLifetime.TryLogError($"[EQP] Repeated native panel hook error suppressed {_suppressedErrors} times.");

        _suppressedErrors = 0;
        _lastError = null;
    }

    private void NoteFailure(Exception ex)
    {
        var key = $"{ex.GetType().FullName}: {ex.Message}";
        if (key == _lastError)
        {
            _suppressedErrors++;
            return;
        }

        if (_suppressedErrors > 0)
            PluginLifetime.TryLogError($"[EQP] Repeated native panel hook error suppressed {_suppressedErrors} times.");

        _suppressedErrors = 0;
        _lastError = key;
        PluginLifetime.TryLogError($"[EQP] Native panel hook failed: {ex}");
    }
}

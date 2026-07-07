using Dalamud.Interface.Windowing;
using EnhancedQuickPanel.Services;
using EnhancedQuickPanel.Services.CustomIcons;
using EnhancedQuickPanel.Services.Localization;
using EnhancedQuickPanel.UI;
using MirageUI.Theme;

namespace EnhancedQuickPanel;

/// <summary>Plugin entry point that wires up services, windows, slash commands, and Dalamud events.</summary>
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Enhanced Quick Panel";

    internal static Configuration C = null!;

    private readonly WindowSystem _windows = new("EnhancedQuickPanel");
    private readonly PanelOverlayWindow _overlayWindow = new();
    private QuickPanelNativeInterceptor? _nativeInterceptor;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        MirageUi.ConfigureTheme(() => MirageColorSettings.CreateDefault());
        MirageUi.Init(pluginInterface, Svc.Texture, Svc.Log);

        I18n.Initialize();

        C = EzConfig.Init<Configuration>();
        C.EnsureDefaults();
        CustomIconRegistry.Initialize();

        _nativeInterceptor = new QuickPanelNativeInterceptor();

        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;

        _windows.AddWindow(_overlayWindow);
        Svc.PluginInterface.UiBuilder.Draw += _windows.Draw;
        Svc.PluginInterface.UiBuilder.Draw += OnDraw;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleOverlay;

        EzConfigGui.Init(new UI.ConfigWindow(), windowType: EzConfigGui.WindowType.Config);
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= EzConfigGui.Open;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;

        EzCmd.Add("/enhancedquickpanel", HandleChatCommand, T("command.enhancedquickpanel.help"));
        EzCmd.Add("/eqp", HandleChatCommand, T("command.eqp.help"));
    }

    private void HandleChatCommand(string command, string args)
    {
        if (args.Trim().Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfig();
            return;
        }

        ToggleOverlay();
    }

    private void ToggleOverlay() => _overlayWindow.ToggleVisibility();

    private static void OnDraw()
    {
        TextCommandExecutor.ProcessPending();
    }

    private static void ToggleConfig()
    {
        if (EzConfigGui.Window == null)
        {
            EzConfigGui.Open();
            return;
        }

        EzConfigGui.Window.IsOpen = !EzConfigGui.Window.IsOpen;
    }

    private static void OnTerritoryChanged(uint _)
    {
        SlotIconResolver.ClearCache();
        QuickPanelUiCache.Invalidate();
        SlotRuntimeCache.Invalidate();
    }

    public void Dispose()
    {
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Svc.PluginInterface.UiBuilder.Draw -= _windows.Draw;
        Svc.PluginInterface.UiBuilder.Draw -= OnDraw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleOverlay;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        _nativeInterceptor?.Dispose();
        TextCommandExecutor.CancelPending();
        CustomIconRegistry.Dispose();
        _windows.RemoveAllWindows();
        MirageUi.Dispose();
        ECommonsMain.Dispose();
        C = null!;
    }
}


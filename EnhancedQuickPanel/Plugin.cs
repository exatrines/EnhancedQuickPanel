using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using EnhancedQuickPanel.Services;
using EnhancedQuickPanel.Services.CustomIcons;
using EnhancedQuickPanel.UI;
using MirageUI.Theme;

namespace EnhancedQuickPanel;

/// <summary>Plugin entry point that wires up services, windows, slash commands, and Dalamud events.</summary>
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Enhanced Quick Panel";

    internal static Configuration Config = null!;
    private static PanelOverlayWindow? _overlayUi;
    private static ConfigurationWindow? _configUi;

    private readonly WindowSystem _windows = new("EnhancedQuickPanel");
    private readonly PanelOverlayWindow _overlayWindow;
    private readonly ConfigurationWindow _configWindow;
    private NativeQuickPanelInterceptor? _nativeInterceptor;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<PluginServices>();
        I18n.Initialize();

        var store = new ConfigurationStore(PluginServices.PluginInterface.GetPluginConfigDirectory());
        ConfigurationStore.Active = store;
        try
        {
            Config = store.Load();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error($"{ex.Message}\n{ex}");
            throw;
        }

        PluginLifetime.Start();

        MirageUi.ConfigureTheme(() => MirageColorSettings.CreateDefault());
        MirageUi.Init(pluginInterface, PluginServices.Texture, PluginServices.Log);

        CustomIconRegistry.Initialize();
        PluginIconStore.Initialize();

        _nativeInterceptor = new NativeQuickPanelInterceptor();
        _overlayWindow = new PanelOverlayWindow();
        _configWindow = new ConfigurationWindow();

        PluginServices.ClientState.TerritoryChanged += OnTerritoryChanged;

        _windows.AddWindow(_overlayWindow);
        _windows.AddWindow(_configWindow);
        _overlayUi = _overlayWindow;
        _configUi = _configWindow;
        PluginServices.PluginInterface.UiBuilder.Draw += _windows.Draw;
        PluginServices.PluginInterface.UiBuilder.Draw += OnDraw;
        PluginServices.PluginInterface.UiBuilder.OpenMainUi += ToggleOverlay;
        PluginServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;

        PluginServices.Commands.AddHandler("/enhancedquickpanel", new CommandInfo(HandleChatCommand)
        {
            HelpMessage = T("command.enhancedquickpanel.help"),
        });
        PluginServices.Commands.AddHandler("/eqp", new CommandInfo(HandleChatCommand)
        {
            HelpMessage = T("command.eqp.help"),
        });
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

    private static void OnDraw()
    {
        TextCommandExecutor.ProcessPending();
    }

    internal static void ToggleOverlay() => _overlayUi?.ToggleVisibility();

    internal static void ToggleConfig()
    {
        if (_configUi == null)
            return;

        _configUi.IsOpen = !_configUi.IsOpen;
    }

    private void OnTerritoryChanged(uint _)
    {
        SlotIconResolver.ClearCache();
        NativeQuickPanelUiCache.Invalidate();
        SlotRuntimeCache.Invalidate();
        PluginShortcuts.Invalidate();
    }

    public void Dispose()
    {
        PluginLifetime.BeginStop();
        PluginServices.Commands.RemoveHandler("/enhancedquickpanel");
        PluginServices.Commands.RemoveHandler("/eqp");
        PluginServices.ClientState.TerritoryChanged -= OnTerritoryChanged;
        PluginServices.PluginInterface.UiBuilder.Draw -= _windows.Draw;
        PluginServices.PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginServices.PluginInterface.UiBuilder.OpenMainUi -= ToggleOverlay;
        PluginServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        _nativeInterceptor?.Dispose();
        TextCommandExecutor.CancelPending();
        PluginIconStore.Dispose();
        CustomIconRegistry.Dispose();
        _windows.RemoveAllWindows();
        MirageUi.Dispose();
        _overlayUi = null;
        _configUi = null;
        Config = null!;
    }
}

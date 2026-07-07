using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;
using MirageUI.Theme;

namespace EnhancedQuickPanel.UI;

/// <summary>Window for importing slots from the game's native quick panel into a new page.</summary>
internal static class NativeQuickPanelImportWindow
{
    private const float WindowBorderRounding = 5f;

    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings;

    private static bool _isOpen;
    private static int _selectedNativePageIndex;
    private static int _previewNativePageIndex = -1;
    private static PanelPage? _previewPage;
    private static Action<int>? _onImported;

    public static bool IsOpen => _isOpen;

    public static void Open(Action<int>? onImported = null)
    {
        _selectedNativePageIndex = 0;
        _previewNativePageIndex = -1;
        _previewPage = null;
        _onImported = onImported;
        _isOpen = true;
    }

    public static void Close()
    {
        _isOpen = false;
        _previewPage = null;
        _previewNativePageIndex = -1;
        _onImported = null;
    }

    public static void Draw()
    {
        if (!_isOpen)
            return;

        using var appearance = new MirageWindowAppearanceScope();
        ImGui.SetNextWindowSize(ComputeWindowSize(), ImGuiCond.Always);

        if (!ImGui.Begin(T("nativeImportWindow.title") + "##eqpNativeImportWindow", ref _isOpen, WindowFlags))
        {
            ImGui.End();
            return;
        }

        DrawContent();
        ImGui.End();

        if (!_isOpen)
            Close();
    }

    private static void DrawContent()
    {
        C.EnsureDefaults();

        if (!NativeQuickPanelReader.IsAvailable)
        {
            MirageUi.Text(T("nativeImportWindow.unavailable"), MirageUi.Color.Secondary);
            return;
        }

        MirageUi.Text(T("nativeImportWindow.addAsNewPage"), MirageUi.Color.Secondary);
        MirageUi.Text(T("nativeImportWindow.selectNativePage"), MirageUi.Color.Secondary);

        var selectedNativePage = _selectedNativePageIndex;
        ImGui.RadioButton(T("contextMenu.nativePage1"), ref selectedNativePage, 0);
        ImGui.SameLine();
        ImGui.RadioButton(T("contextMenu.nativePage2"), ref selectedNativePage, 1);
        ImGui.SameLine();
        ImGui.RadioButton(T("contextMenu.nativePage3"), ref selectedNativePage, 2);
        ImGui.SameLine();
        ImGui.RadioButton(T("contextMenu.nativePage4"), ref selectedNativePage, 3);
        _selectedNativePageIndex = selectedNativePage;

        MirageUi.PaddedSeparator();
        MirageUi.Text(T("nativeImportWindow.preview"), MirageUi.Color.Secondary);

        if (EnsurePreview(_selectedNativePageIndex) && _previewPage != null)
            PanelPagePreview.Draw(_previewPage, "nativeImport");
        else
            MirageUi.Text(T("nativeImportWindow.previewUnavailable"), MirageUi.Color.Secondary);

        MirageUi.PaddedSeparator();

        if (ImGui.Button(T("common.import")))
            ImportSelectedPage();
    }

    private static bool EnsurePreview(int nativePageIndex)
    {
        if (_previewNativePageIndex == nativePageIndex && _previewPage != null)
            return true;

        if (!NativeQuickPanelImporter.TryBuildNativePagePreview(nativePageIndex, out var page))
        {
            _previewPage = null;
            _previewNativePageIndex = -1;
            return false;
        }

        _previewPage = page;
        _previewNativePageIndex = nativePageIndex;
        return true;
    }

    private static void ImportSelectedPage()
    {
        if (!NativeQuickPanelImporter.TryImportNativePageAsNewPage(_selectedNativePageIndex, out var newPageIndex))
        {
            Notify.Warning(T("contextMenu.importNativeFailed"));
            return;
        }

        EzConfig.Save();
        Notify.Success(T("contextMenu.importNativeSuccess", _selectedNativePageIndex + 1));
        _onImported?.Invoke(newPageIndex);
        Close();
    }

    private static Vector2 ComputeWindowSize()
    {
        C.EnsureDefaults();
        var pad = C.WindowPadding;
        var style = ImGui.GetStyle();
        var slotSize = C.SlotSize;
        var spacing = C.SlotPadding;
        var gridSpan = Configuration.GridSize * slotSize
            + Math.Max(0, Configuration.GridSize - 1) * spacing;
        var titleBarH = ImGui.GetFontSize() + style.FramePadding.Y * 2f;
        var controlsH = ImGui.GetFrameHeightWithSpacing() * 4.5f + style.ItemSpacing.Y * 3f;

        return new Vector2(
            Math.Max(320f, gridSpan + pad * 2f),
            gridSpan + controlsH + pad * 2f + titleBarH);
    }

    /// <summary>Pushes MirageUI window styling for the import window and restores it on dispose.</summary>
    private readonly struct MirageWindowAppearanceScope : IDisposable
    {
        private readonly ImRaii.ColorDisposable? _theme;
        private readonly ImRaii.ColorDisposable? _popupBg;
        private readonly ImRaii.ColorDisposable? _border;
        private readonly ImRaii.ColorDisposable? _button;
        private readonly ImRaii.ColorDisposable? _buttonHovered;
        private readonly ImRaii.ColorDisposable? _buttonActive;
        private readonly ImRaii.ColorDisposable? _frameBgActive;
        private readonly ImRaii.StyleDisposable _windowBorderSize;
        private readonly ImRaii.StyleDisposable _windowRounding;
        private readonly ImRaii.StyleDisposable _windowPadding;
        private readonly ImRaii.StyleDisposable _itemSpacing;

        public MirageWindowAppearanceScope()
        {
            MirageTheme.EnsureDefaultsCaptured();
            var settings = MirageTheme.ResolveAppliedColors();
            _theme = MirageTheme.PushCustom(settings);
            _popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, settings.WindowBg);
            _border = ImRaii.PushColor(ImGuiCol.Border, settings.GetColor(MirageUi.Color.Accent));
            _button = ImRaii.PushColor(ImGuiCol.Button, settings.Header);
            _buttonHovered = ImRaii.PushColor(ImGuiCol.ButtonHovered, settings.HeaderHovered);
            _buttonActive = ImRaii.PushColor(ImGuiCol.ButtonActive, settings.HeaderActive);
            _frameBgActive = ImRaii.PushColor(ImGuiCol.FrameBgActive, settings.FrameBgHovered);

            C.EnsureDefaults();
            _windowBorderSize = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0f);
            _windowRounding = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, WindowBorderRounding);
            _windowPadding = ImRaii.PushStyle(
                ImGuiStyleVar.WindowPadding,
                new Vector2(C.WindowPadding, C.WindowPadding));
            _itemSpacing = ImRaii.PushStyle(
                ImGuiStyleVar.ItemSpacing,
                new Vector2(C.SlotPadding, C.SlotPadding));
        }

        public void Dispose()
        {
            _itemSpacing.Dispose();
            _windowPadding.Dispose();
            _windowRounding.Dispose();
            _windowBorderSize.Dispose();
            _frameBgActive?.Dispose();
            _buttonActive?.Dispose();
            _buttonHovered?.Dispose();
            _button?.Dispose();
            _border?.Dispose();
            _popupBg?.Dispose();
            MirageTheme.Pop(_theme);
        }
    }
}

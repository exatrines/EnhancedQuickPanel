using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Floating popup that lists selectable pages when the page name is clicked in the overlay.</summary>
internal static class PageListWindow
{
    private const float WindowBorderRounding = 5f;

    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.AlwaysAutoResize;

    private static bool _isOpen;

    public static bool IsOpen => _isOpen;

    public static void Toggle() => _isOpen = !_isOpen;

    public static void Close() => _isOpen = false;

    public static void Draw(ref int selectedPage)
    {
        if (!_isOpen)
            return;

        C.EnsureDefaults();
        var style = C.PanelUi;
        style.EnsureDefaults();

        using (new PanelUiDropdownStyleScope(style))
        using (PanelUiTextStyle.PushText(style))
        {
            using var windowBg = ImRaii.PushColor(ImGuiCol.WindowBg, style.DropdownBgColor);

            ImGui.SetNextWindowSize(new Vector2(0f, 0f), ImGuiCond.FirstUseEver);

            if (!ImGui.Begin("ページ一覧##eqpPageListWindow", ref _isOpen, WindowFlags))
            {
                ImGui.End();
                return;
            }

            var width = Math.Max(240f, ImGui.GetContentRegionAvail().X);
            PageListPanel.Draw(ref selectedPage, C.Pages, style, width);
            PageReorderDragHandler.ProcessEndOfFrame(C.Pages, ref selectedPage);

            DrawWindowBorder();
            ImGui.End();
        }
    }

    private static void DrawWindowBorder()
    {
        var thickness = C.WindowBorderThickness;
        if (thickness <= 0f)
            return;

        var color = ImGui.ColorConvertFloat4ToU32(C.WindowBorderColor);
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        ImGui.GetWindowDrawList().AddRect(
            min,
            max,
            color,
            WindowBorderRounding,
            ImDrawFlags.RoundCornersAll,
            thickness);
    }
}


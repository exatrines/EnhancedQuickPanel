using Dalamud.Interface;

namespace EnhancedQuickPanel.UI;

/// <summary>Chrome for collapsing the panel grid. Not a slot action.</summary>
internal static class PanelCollapse
{
    public static FontAwesomeIcon Icon =>
        Config.IsCollapsed ? FontAwesomeIcon.ThLarge : FontAwesomeIcon.AngleDown;

    public static string Label =>
        T(Config.IsCollapsed ? "shortcut.expand" : "shortcut.collapse");
}

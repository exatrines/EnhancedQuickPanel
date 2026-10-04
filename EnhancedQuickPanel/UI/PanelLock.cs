using Dalamud.Interface;

namespace EnhancedQuickPanel.UI;

/// <summary>Chrome for locking the overlay position. Not a slot action.</summary>
internal static class PanelLock
{
    public static bool IsLocked => Config.OverlayLocked;

    public static FontAwesomeIcon Icon =>
        IsLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock;

    public static FontAwesomeIcon ActionIcon =>
        IsLocked ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock;

    public static string Label =>
        T(IsLocked ? "shortcut.unlock" : "shortcut.lock");

    public static void Toggle()
    {
        Config.OverlayLocked = !Config.OverlayLocked;
        Config.Save();
    }
}

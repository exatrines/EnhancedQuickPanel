using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Moves the overlay from empty padding or item drags, unless position is locked.</summary>
internal static class OverlayDrag
{
    private static bool _isDragging;
    private static bool _moveRequested;
    private static Vector2 _dragMouseStart;
    private static Vector2 _dragWindowStart;
    private static Vector2 _pressPos;
    private static bool _pressMoved;

    public static void Handle()
    {
        if (SlotDragDropHandler.IsGameDragActive
            || SlotSwapDragHandler.IsInternalDragActive
            || PageReorderDragHandler.IsDragging)
        {
            _moveRequested = false;
            if (_isDragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _isDragging = false;
                Config.Save();
            }

            return;
        }

        if (PanelLock.IsLocked)
        {
            _moveRequested = false;
            if (_isDragging)
            {
                _isDragging = false;
                Config.Save();
            }

            return;
        }

        var io = ImGui.GetIO();

        if (_moveRequested)
        {
            _moveRequested = false;
            if (!_isDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                Begin(io.MousePos);
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_isDragging)
                Config.Save();

            _isDragging = false;
        }

        if (_isDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var delta = io.MousePos - _dragMouseStart;
                Config.OverlayPosX = _dragWindowStart.X + delta.X;
                Config.OverlayPosY = _dragWindowStart.Y + delta.Y;
            }

            return;
        }

        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            return;

        if (ImGui.IsAnyItemHovered() || ImGui.IsAnyItemActive())
            return;

        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId))
            return;

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        Begin(io.MousePos);
    }

    public static bool ConsumeClickWithoutDrag(bool imguiClicked, bool alwaysMoveWindow = false)
    {
        var io = ImGui.GetIO();
        if (ImGui.IsItemActivated())
        {
            _pressPos = io.MousePos;
            _pressMoved = false;
        }

        if (ImGui.IsItemActive() && !_pressMoved)
        {
            var delta = io.MousePos - _pressPos;
            var threshold = Math.Max(4f, io.MouseDragThreshold);
            if (delta.LengthSquared() >= threshold * threshold)
            {
                _pressMoved = true;
                if (!PanelLock.IsLocked && (alwaysMoveWindow || !Config.DisableOverlayDragFromItems))
                    _moveRequested = true;
            }
        }

        return imguiClicked && !_pressMoved;
    }

    private static void Begin(Vector2 mousePos)
    {
        _isDragging = true;
        _dragMouseStart = mousePos;
        _dragWindowStart = new Vector2(Config.OverlayPosX, Config.OverlayPosY);
    }
}

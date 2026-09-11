using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EnhancedQuickPanel;

/// <summary>Typed ImGui drag-and-drop payloads for slot and page reordering.</summary>
internal static class ImGuiDragDrop
{
    public static unsafe void SetDragDropPayload<T>(string type, T data, ImGuiCond cond = 0)
        where T : struct
    {
        var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref data, 1));
        ImGui.SetDragDropPayload(type, span, cond);
    }

    public static unsafe bool AcceptDragDropPayload<T>(
        string type,
        out T payload,
        ImGuiDragDropFlags flags = ImGuiDragDropFlags.None)
        where T : struct
    {
        var loaded = ImGui.AcceptDragDropPayload(type, flags);
        if (loaded.IsNull)
        {
            payload = default;
            return false;
        }

        payload = Unsafe.Read<T>(loaded.Data);
        return true;
    }
}

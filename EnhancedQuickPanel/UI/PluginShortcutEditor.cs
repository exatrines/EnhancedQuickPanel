using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

internal static class PluginShortcutEditor
{
    public static void DrawUnavailableWarning(PanelSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.PluginInternalName) || PluginShortcuts.Find(slot) != null)
            return;
        ImGui.Spacing();
        ImGui.TextWrapped(T("shortcut.unavailable"));
    }

    public static void DrawCurrentName(PanelSlot slot, float? width = null)
    {
        var preview = PreviewLabel(slot);
        if (width is { } nameWidth)
            ImGui.PushItemWidth(nameWidth);
        using (ImRaii.Disabled())
            ImGui.InputText("##eqpSlotEditorPluginName", ref preview, 128, ImGuiInputTextFlags.ReadOnly);
        if (width is not null)
            ImGui.PopItemWidth();
    }

    private static string PreviewLabel(PanelSlot slot)
    {
        var installed = PluginShortcuts.ListPickerEntries();
        for (var i = 0; i < installed.Count; i++)
        {
            if (!PluginShortcuts.IsSelected(installed[i], slot))
                continue;
            return PluginShortcuts.PickerLabel(installed[i], installed);
        }

        return string.IsNullOrWhiteSpace(slot.PluginInternalName)
            ? T("slot.kind.plugin")
            : slot.PluginInternalName;
    }
}

using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

internal static class PluginShortcutEditor
{
    public static void DrawPluginCombo(PanelSlot slot, float? width = null, string id = "##eqpPlugin")
    {
        var selected = PluginShortcuts.Find(slot.PluginInternalName);
        var preview = selected?.Name
            ?? (string.IsNullOrWhiteSpace(slot.PluginInternalName)
                ? T("slot.kind.plugin")
                : slot.PluginInternalName);
        if (width is { } comboWidth)
            ImGui.PushItemWidth(comboWidth);
        if (ImGui.BeginCombo(id, preview))
        {
            foreach (var plugin in PluginServices.PluginInterface.InstalledPlugins.OrderBy(p => p.Name))
            {
                if (!PluginShortcuts.AppearsInPicker(plugin, slot.PluginInternalName))
                    continue;
                if (ImGui.Selectable($"{plugin.Name}##{plugin.InternalName}", plugin.InternalName == slot.PluginInternalName)
                    && plugin.InternalName != slot.PluginInternalName)
                {
                    slot.PluginInternalName = plugin.InternalName;
                    slot.IconId = 0;
                    Config.Save();
                }
            }
            ImGui.EndCombo();
        }
        if (width is not null)
            ImGui.PopItemWidth();
    }

    public static void DrawUnavailableWarning(PanelSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.PluginInternalName))
            return;
        if (PluginShortcuts.Find(slot.PluginInternalName) != null)
            return;
        ImGui.TextWrapped(T("shortcut.unavailable"));
    }
}

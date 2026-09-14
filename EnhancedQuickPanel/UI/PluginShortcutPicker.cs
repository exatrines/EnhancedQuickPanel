using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Inline plugin list for the slot editor, with search.</summary>
internal static class PluginShortcutPicker
{
    private const float IconSize = 28f;
    private const float RowHeight = 40f;

    private static string _search = string.Empty;
    private static PanelSlot? _boundSlot;

    public static void DrawEmbedded(PanelSlot slot)
    {
        if (!ReferenceEquals(_boundSlot, slot))
        {
            _boundSlot = slot;
            _search = string.Empty;
        }

        ImGui.Spacing();
        var search = _search;
        ImGui.SetNextItemWidth(-1f);
        using (PanelUiTextStyle.PushInputText(Config.PanelUi, "##eqpPluginSearch"))
        {
            if (ImGui.InputTextWithHint("##eqpPluginSearch", T("pluginPicker.search"), ref search, 128))
                _search = search;
            PanelUiTextStyle.NotifyInputHover("##eqpPluginSearch");
        }

        var style = Config.PanelUi;
        var listHeight = Math.Max(80f, ImGui.GetContentRegionAvail().Y);
        var installed = PluginShortcuts.ListPickerEntries();
        var shown = 0;
        using (ImRaii.PushColor(ImGuiCol.ChildBg, style.FieldBgColor))
        using (ImRaii.PushColor(ImGuiCol.Border, style.ButtonBgColor))
        {
            if (ImGui.BeginChild("##eqpPluginList", new Vector2(-1f, listHeight), true))
            {
                for (var i = 0; i < installed.Count; i++)
                {
                    var entry = installed[i];
                    if (!Matches(entry, slot))
                        continue;
                    shown++;
                    DrawRow(slot, entry, i);
                }

                if (shown == 0)
                    ImGui.TextDisabled(T("pluginPicker.empty"));
            }

            ImGui.EndChild();
        }
    }

    private static bool Matches(InstalledPluginEntry entry, PanelSlot slot)
    {
        if (!PluginShortcuts.AppearsInPicker(entry, slot))
            return false;
        if (string.IsNullOrWhiteSpace(_search))
            return true;

        var query = _search.Trim();
        return Contains(entry.Plugin.Name, query)
            || Contains(entry.Plugin.InternalName, query)
            || Contains(PluginShortcuts.ReadAuthor(entry.Plugin), query);
    }

    private static bool Contains(string value, string query) =>
        !string.IsNullOrEmpty(value)
        && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static void DrawRow(PanelSlot slot, InstalledPluginEntry entry, int index)
    {
        var plugin = entry.Plugin;
        var selected = PluginShortcuts.IsSelected(entry, slot);
        var id = $"##eqpPluginRow-{entry.WorkingId}-{index}";
        var avail = ImGui.GetContentRegionAvail().X;
        if (ImGui.Selectable(id, selected, ImGuiSelectableFlags.None, new Vector2(avail, RowHeight)))
            PluginShortcuts.Assign(slot, entry);

        var min = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();
        var iconMin = min + new Vector2(4f, (RowHeight - IconSize) * 0.5f);
        var iconMax = iconMin + new Vector2(IconSize, IconSize);
        if (!(PluginShortcuts.TryGetIcon(entry, out var icon)
            && SafeTextureDraw.TryAddImage(drawList, icon, iconMin, iconMax, uint.MaxValue)))
        {
            var mark = "?";
            var markSize = ImGui.CalcTextSize(mark);
            drawList.AddText(iconMin + (iconMax - iconMin - markSize) * 0.5f, ImGui.GetColorU32(ImGuiCol.TextDisabled), mark);
        }

        var textX = iconMax.X + 8f - min.X;
        var textColor = plugin.IsLoaded
            ? ImGui.GetColorU32(ImGuiCol.Text)
            : ImGui.GetColorU32(ImGuiCol.TextDisabled);
        var name = plugin.IsLoaded ? plugin.Name : T("slot.tooltip.pluginDisabled", plugin.Name);
        if (plugin.IsDev)
            name += $" ({T("pluginPicker.dev")})";
        drawList.AddText(min + new Vector2(textX, 4f), textColor, name);

        var author = PluginShortcuts.ReadAuthor(plugin);
        var version = plugin.Version?.ToString() ?? string.Empty;
        var detail = author;
        if (!string.IsNullOrEmpty(version))
            detail = string.IsNullOrEmpty(detail) ? version : $"{author}  {version}";
        if (!string.IsNullOrEmpty(detail))
        {
            drawList.AddText(
                min + new Vector2(textX, 4f + ImGui.GetTextLineHeight()),
                ImGui.GetColorU32(ImGuiCol.TextDisabled),
                detail);
        }
    }
}

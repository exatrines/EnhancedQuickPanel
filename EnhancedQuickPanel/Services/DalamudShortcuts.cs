using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

internal readonly record struct DalamudCommand(
    DalamudShortcutKind Kind,
    string LabelKey,
    FontAwesomeIcon Icon,
    string Command);

// Opens Dalamud windows by running their slash commands.
internal static class DalamudShortcuts
{
    public static readonly DalamudCommand[] All =
    [
        new(DalamudShortcutKind.Plugins, "slot.dalamud.plugins", FontAwesomeIcon.Plug, "/xlplugins"),
        new(DalamudShortcutKind.Settings, "slot.dalamud.settings", FontAwesomeIcon.Cog, "/xlsettings"),
        new(DalamudShortcutKind.Data, "slot.dalamud.data", FontAwesomeIcon.Database, "/xldata"),
        new(DalamudShortcutKind.Log, "slot.dalamud.log", FontAwesomeIcon.FileAlt, "/xllog"),
    ];

    private static ISharedImmediateTexture? _baseTexture;
    private static bool _baseMissing;

    public static bool TryGet(DalamudShortcutKind kind, out DalamudCommand preset)
    {
        preset = All.FirstOrDefault(entry => entry.Kind == kind);
        return preset.Kind == kind && kind != DalamudShortcutKind.None;
    }

    public static string DisplayName(PanelSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.Label))
            return slot.Label.Trim();
        return TryGet(slot.DalamudShortcut, out var preset)
            ? T(preset.LabelKey)
            : T("slot.kind.dalamud");
    }

    public static void DrawCombo(PanelSlot slot, float? width = null, string id = "##eqpDalamud")
    {
        var preview = TryGet(slot.DalamudShortcut, out var selected)
            ? T(selected.LabelKey)
            : T("slot.kind.dalamud");
        if (width is { } comboWidth)
            ImGui.PushItemWidth(comboWidth);
        if (ImGui.BeginCombo(id, preview))
        {
            foreach (var entry in All)
            {
                if (ImGui.Selectable(T(entry.LabelKey), entry.Kind == slot.DalamudShortcut)
                    && entry.Kind != slot.DalamudShortcut)
                {
                    slot.Kind = PanelSlotKind.Dalamud;
                    slot.DalamudShortcut = entry.Kind;
                    slot.TextBody = string.Empty;
                    slot.IconId = 0;
                    Config.Save();
                }
            }
            ImGui.EndCombo();
        }
        if (width is not null)
            ImGui.PopItemWidth();
    }

    public static void Execute(PanelSlot slot)
    {
        if (!TryGet(slot.DalamudShortcut, out var preset))
            return;

        TextCommandExecutor.Execute(preset.Command);
    }

    public static bool TryDrawIcon(ImDrawListPtr drawList, Vector2 min, Vector2 max, PanelSlot slot, uint tint)
    {
        if (slot.Kind != PanelSlotKind.Dalamud || slot.IconId != 0)
            return false;

        if (TryGetBaseTexture(out var baseTexture))
            SafeTextureDraw.TryAddImage(drawList, baseTexture, min, max, tint);

        var icon = TryGet(slot.DalamudShortcut, out var preset) ? preset.Icon : FontAwesomeIcon.PuzzlePiece;
        var iconText = icon.ToIconString();
        var font = UiBuilder.IconFont;
        var slotSize = max - min;
        var fontSize = Math.Max(8f, slotSize.Y * 0.3f);
        Vector2 textSize;
        using (ImRaii.PushFont(font))
            textSize = ImGui.CalcTextSize(iconText) * (fontSize / ImGui.GetFontSize());
        var inset = Math.Max(2f, slotSize.X * 0.08f);
        var pos = new Vector2(max.X - inset - textSize.X, max.Y - inset - textSize.Y);
        drawList.AddText(font, fontSize, pos, tint, iconText);
        return true;
    }

    private static bool TryGetBaseTexture(out IDalamudTextureWrap texture)
    {
        texture = null!;
        if (_baseMissing)
            return false;

        try
        {
            if (_baseTexture == null)
            {
                var path = Path.Combine(
                    PluginServices.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
                    "Data",
                    "dalamud-shortcut-base.png");
                if (!File.Exists(path))
                {
                    _baseMissing = true;
                    return false;
                }

                _baseTexture = PluginServices.Texture.GetFromFile(path);
            }

            var wrap = _baseTexture.GetWrapOrDefault();
            if (wrap == null || wrap.Handle == 0)
                return false;

            texture = wrap;
            return true;
        }
        catch (Exception ex)
        {
            _baseMissing = true;
            PluginServices.Log.Warning($"[EQP] Dalamud shortcut base icon failed: {ex.Message}");
            return false;
        }
    }
}

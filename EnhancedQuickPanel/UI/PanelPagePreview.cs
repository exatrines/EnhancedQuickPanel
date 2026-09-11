using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using EnhancedQuickPanel.Models;
using EnhancedQuickPanel.Services;

namespace EnhancedQuickPanel.UI;

/// <summary>Renders a non-interactive preview of a page's slots for import and preview dialogs.</summary>
internal static class PanelPagePreview
{
    public static void Draw(PanelPage page, string blockId)
    {
        Config.EnsureDefaults();

        if (!GameModuleGuard.IsClientReady)
        {
            MirageUi.Text(
                "プレビューにはゲーム内ログインが必要です。",
                MirageUi.Color.Secondary);
            return;
        }

        var slotSize = Config.SlotSize;
        var spacing = Config.SlotPadding;
        var gridSpan = PanelLayout.ComputeSpan(Configuration.GridSize, slotSize, spacing);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(spacing, spacing));
        using (ImRaii.Child($"##eqpPagePreview/{blockId}", new Vector2(gridSpan, gridSpan), false))
        {
            var origin = ImGui.GetCursorScreenPos();

            for (var row = 0; row < Configuration.GridSize; row++)
            {
                for (var col = 0; col < Configuration.GridSize; col++)
                {
                    if (col > 0)
                        ImGui.SameLine(0f, spacing);

                    var index = row * Configuration.GridSize + col;
                    var slot = index < page.Slots.Count ? page.Slots[index] : new PanelSlot();
                    DrawSlot(slot, slotSize);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.InvisibleButton($"##eqpPagePreviewBlock/{blockId}", new Vector2(gridSpan, gridSpan));
        }

        ImGui.PopStyleVar();
    }

    private static void DrawSlot(PanelSlot slot, float slotSize)
    {
        var size = new Vector2(slotSize, slotSize);
        ImGui.Dummy(size);
        var topLeft = ImGui.GetItemRectMin();
        var bottomRight = topLeft + size;
        var drawList = ImGui.GetWindowDrawList();

        var icon = SlotIconResolver.ResolveIcon(slot);
        var overlay = slot.Kind switch
        {
            PanelSlotKind.Macro => new SlotOverlayInfo(false, 0, false, true),
            PanelSlotKind.Action => SlotOverlayResolver.Resolve(slot, icon, SlotRuntimeState.Default),
            _ => SlotOverlayInfo.None,
        };
        var pluginVisual = PluginShortcuts.ResolveVisual(slot);
        var isGrayedOut = PluginShortcuts.IsDimmed(pluginVisual);
        var iconTint = isGrayedOut
            ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f))
            : uint.MaxValue;
        SlotChromeDrawer.DrawBaseFrame(drawList, topLeft, bottomRight, iconGrayedOut: isGrayedOut);

        if ((PluginShortcuts.TryGetIcon(slot, out var texture)
            || (icon.IsValid && SlotTextureResolver.TryGetSlotTexture(icon, out texture)))
            && SafeTextureDraw.TryAddImage(drawList, texture, topLeft, bottomRight, iconTint))
        {
            SlotChromeDrawer.DrawIconFrame(drawList, topLeft, bottomRight, isGrayedOut);
        }
        else if (DalamudShortcuts.TryDrawIcon(drawList, topLeft, bottomRight, slot, iconTint))
        {
            SlotChromeDrawer.DrawIconFrame(drawList, topLeft, bottomRight, isGrayedOut);
        }

        if (overlay.ShowMacroIndicator)
            DrawMacroIndicator(drawList, topLeft, bottomRight);

        if (overlay.ShowActionCharges)
            DrawCornerText(drawList, topLeft, bottomRight, overlay.ActionCharges.ToString(), Config.ChargeLabelStyle);
        else if (overlay.ShowQuantity)
            DrawCornerText(drawList, topLeft, bottomRight, $"x{overlay.Quantity}", Config.QuantityLabelStyle);
    }

    private static void DrawMacroIndicator(ImDrawListPtr drawList, Vector2 slotMin, Vector2 slotMax)
    {
        var iconText = FontAwesomeIcon.Cog.ToIconString();
        var iconFont = UiBuilder.IconFont;
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(Config.SlotSize, Config.MacroGearLabelStyle);
        Vector2 textSize;
        using (ImRaii.PushFont(iconFont))
            textSize = ImGui.CalcTextSize(iconText) * (fontSize / ImGui.GetFontSize());

        var pos = new Vector2(slotMax.X - textSize.X + 5f, slotMin.Y - 4f);
        SlotOverlayTextRenderer.Draw(
            drawList,
            iconFont,
            fontSize,
            pos,
            iconText,
            Config.MacroGearLabelStyle,
            OverlayLabelLetterSpacing.Default,
            isGrayedOut: false);
    }

    private static void DrawCornerText(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        string text,
        OverlayLabelStyleConfig style)
    {
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(Config.SlotSize, style);
        var textSize = SlotOverlayTextRenderer.MeasureTextSize(
            text,
            ImGui.GetFont(),
            fontSize,
            OverlayLabelLetterSpacing.Default);
        var pos = new Vector2(slotMax.X - textSize.X - 2f, slotMax.Y - textSize.Y - 1f)
            + new Vector2(3f, 4f);

        SlotOverlayTextRenderer.Draw(
            drawList,
            ImGui.GetFont(),
            fontSize,
            pos,
            text,
            style,
            OverlayLabelLetterSpacing.Default,
            isGrayedOut: false);
    }
}

using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws the cooldown sweep and remaining-time text on a slot.</summary>
internal static class SlotCooldownRenderer
{
    public static void Draw(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        SlotCooldownInfo cooldown)
    {
        if (!cooldown.IsActive)
            return;

        DrawCooldownCircle(drawList, slotMin, slotMax, cooldown.ElapsedFraction);
        DrawCooldownSeconds(drawList, slotMin, slotMax, cooldown.SecondsRemaining);
    }

    private static void DrawCooldownCircle(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        float elapsedFraction)
    {
        var fraction = Math.Clamp(elapsedFraction, 0f, 1f);
        if (fraction <= 0f)
            return;

        var center = new Vector2(
            (slotMin.X + slotMax.X) * 0.5f,
            (slotMin.Y + slotMax.Y) * 0.5f);
        var radius = Math.Min(slotMax.X - slotMin.X, slotMax.Y - slotMin.Y) * 0.5f;
        const float startAngle = -MathF.PI / 2f;
        var endAngle = startAngle + MathF.Tau * fraction;
        var fillColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.58f));

        drawList.PathClear();
        drawList.PathLineTo(center);
        drawList.PathArcTo(center, radius, startAngle, endAngle, 48);
        drawList.PathFillConvex(fillColor);
    }

    private static void DrawCooldownSeconds(
        ImDrawListPtr drawList,
        Vector2 slotMin,
        Vector2 slotMax,
        float secondsRemaining)
    {
        if (secondsRemaining <= 0.05f)
            return;

        var text = FormatRemainingSeconds(secondsRemaining);
        var style = C.CooldownLabelStyle;
        var fontSize = SlotOverlayFontSizeResolver.ResolveFontSize(slotMax.X - slotMin.X, style);
        var textSize = SlotOverlayTextRenderer.MeasureTextSize(
            text,
            ImGui.GetFont(),
            fontSize,
            OverlayLabelLetterSpacing.Cooldown);
        var pos = new Vector2(
            (slotMin.X + slotMax.X - textSize.X) * 0.5f,
            (slotMin.Y + slotMax.Y - textSize.Y) * 0.5f);

        SlotOverlayTextRenderer.Draw(
            drawList,
            ImGui.GetFont(),
            fontSize,
            pos,
            text,
            style,
            OverlayLabelLetterSpacing.Cooldown,
            isGrayedOut: false);
    }

    private static string FormatRemainingSeconds(float secondsRemaining)
    {
        if (secondsRemaining >= 10f)
            return ((int)Math.Ceiling(secondsRemaining)).ToString();

        if (secondsRemaining > 0.05f)
            return ((int)Math.Ceiling(Math.Max(1f, secondsRemaining))).ToString();

        return "1";
    }
}


using Dalamud.Bindings.ImGui;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws overlay label text (cooldown, charges, quantity) on a slot.</summary>
internal static class SlotOverlayTextRenderer
{
    private static readonly Vector2[] EdgeOffsets =
    [
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, 1f),
        new(0f, -1f),
        new(0.707f, 0.707f),
        new(-0.707f, 0.707f),
        new(0.707f, -0.707f),
        new(-0.707f, -0.707f),
    ];

    public static Vector2 MeasureTextSize(string text, ImFontPtr font, float fontSize, float letterSpacing)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var fontScale = fontSize / ImGui.GetFontSize();
        if (Math.Abs(letterSpacing) < 0.001f)
            return ImGui.CalcTextSize(text) * fontScale;

        var width = 0f;
        var height = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            var charSize = MeasureCharacterSize(text[i], fontScale);
            if (i > 0)
                width += letterSpacing;

            width += charSize.X;
            height = Math.Max(height, charSize.Y);
        }

        return new Vector2(width, height);
    }

    public static void Draw(
        ImDrawListPtr drawList,
        ImFontPtr font,
        float fontSize,
        Vector2 pos,
        string text,
        OverlayLabelStyleConfig style,
        float letterSpacing,
        bool isGrayedOut = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var textColor = ToColorU32(ApplyGray(style.TextColor, isGrayedOut));
        var edgeColor = ToColorU32(ApplyGray(style.EdgeColor, isGrayedOut));

        if (style.EdgeThickness > 0.01f && style.EdgeColor.W > 0f)
        {
            foreach (var offset in EdgeOffsets)
            {
                DrawTextLayer(
                    drawList,
                    font,
                    fontSize,
                    pos + offset * style.EdgeThickness,
                    text,
                    edgeColor,
                    letterSpacing);
            }
        }

        DrawTextLayer(drawList, font, fontSize, pos, text, textColor, letterSpacing);
    }

    private static void DrawTextLayer(
        ImDrawListPtr drawList,
        ImFontPtr font,
        float fontSize,
        Vector2 pos,
        string text,
        uint color,
        float letterSpacing)
    {
        if (Math.Abs(letterSpacing) < 0.001f)
        {
            drawList.AddText(font, fontSize, pos, color, text);
            return;
        }

        var x = pos.X;
        var fontScale = fontSize / ImGui.GetFontSize();
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i].ToString();
            drawList.AddText(font, fontSize, new Vector2(x, pos.Y), color, character);
            x += MeasureCharacterSize(text[i], fontScale).X + letterSpacing;
        }
    }

    private static Vector2 MeasureCharacterSize(char character, float fontScale) =>
        ImGui.CalcTextSize(character.ToString()) * fontScale;

    private static Vector4 ApplyGray(Vector4 color, bool isGrayedOut) =>
        isGrayedOut
            ? new Vector4(color.X * 0.5f, color.Y * 0.5f, color.Z * 0.5f, color.W)
            : color;

    private static uint ToColorU32(Vector4 color) =>
        ImGui.ColorConvertFloat4ToU32(color);
}


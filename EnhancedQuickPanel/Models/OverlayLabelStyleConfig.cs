namespace EnhancedQuickPanel.Models;

/// <summary>Text and edge styling for a slot overlay label (cooldown, charges, quantity).</summary>
public sealed class OverlayLabelStyleConfig
{
    public float TextRed { get; set; } = 1f;

    public float TextGreen { get; set; } = 1f;

    public float TextBlue { get; set; } = 1f;

    public float TextAlpha { get; set; } = 1f;

    public float EdgeRed { get; set; }

    public float EdgeGreen { get; set; }

    public float EdgeBlue { get; set; }

    public float EdgeAlpha { get; set; } = 1f;

    public float EdgeThickness { get; set; }

    public float TextSizeScale { get; set; } = 1f;

    public Vector4 TextColor => new(TextRed, TextGreen, TextBlue, TextAlpha);

    public Vector4 EdgeColor => new(EdgeRed, EdgeGreen, EdgeBlue, EdgeAlpha);

    public void SetTextColor(Vector4 color)
    {
        TextRed = color.X;
        TextGreen = color.Y;
        TextBlue = color.Z;
        TextAlpha = color.W;
    }

    public void SetEdgeColor(Vector4 color)
    {
        EdgeRed = color.X;
        EdgeGreen = color.Y;
        EdgeBlue = color.Z;
        EdgeAlpha = color.W;
    }

    public void SetTextColorRgb(Vector3 color) => SetTextColor(new Vector4(color, 1f));

    public void SetEdgeColorRgb(Vector3 color) => SetEdgeColor(new Vector4(color, 1f));

    public float ScaleFontSize(float baseFontSize) => baseFontSize * TextSizeScale;

    public static OverlayLabelStyleConfig CreateDefaultText() => new();

    public static OverlayLabelStyleConfig CreateDefaultMacroGear() => new();
}


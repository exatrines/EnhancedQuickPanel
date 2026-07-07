namespace EnhancedQuickPanel.Models;

/// <summary>Colors and padding for the overlay context menu.</summary>
public sealed class ContextMenuStyleConfig
{
    public float Padding { get; set; } = 2f;

    public float Width { get; set; } = 100f;

    public float BgRed { get; set; } = 0.08f;

    public float BgGreen { get; set; } = 0.08f;

    public float BgBlue { get; set; } = 0.08f;

    public float BgAlpha { get; set; } = 0.9f;

    public float TextRed { get; set; } = 1f;

    public float TextGreen { get; set; } = 1f;

    public float TextBlue { get; set; } = 1f;

    public float TextAlpha { get; set; } = 1f;

    public float TextHoverRed { get; set; } = 1f;

    public float TextHoverGreen { get; set; } = 1f;

    public float TextHoverBlue { get; set; } = 1f;

    public float TextHoverAlpha { get; set; } = 1f;

    public float ButtonBgRed { get; set; } = 0.2f;

    public float ButtonBgGreen { get; set; } = 0.2f;

    public float ButtonBgBlue { get; set; } = 0.2f;

    public float ButtonBgAlpha { get; set; } = 1f;

    public float ButtonBgHoverRed { get; set; } = 0.31f;

    public float ButtonBgHoverGreen { get; set; } = 0.31f;

    public float ButtonBgHoverBlue { get; set; } = 0.31f;

    public float ButtonBgHoverAlpha { get; set; } = 1f;

    public Vector4 BgColor => new(BgRed, BgGreen, BgBlue, BgAlpha);

    public Vector4 TextColor => new(TextRed, TextGreen, TextBlue, TextAlpha);

    public Vector4 TextHoverColor => new(TextHoverRed, TextHoverGreen, TextHoverBlue, TextHoverAlpha);

    public Vector4 ButtonBgColor => new(ButtonBgRed, ButtonBgGreen, ButtonBgBlue, ButtonBgAlpha);

    public Vector4 ButtonBgHoverColor =>
        new(ButtonBgHoverRed, ButtonBgHoverGreen, ButtonBgHoverBlue, ButtonBgHoverAlpha);

    public void SetBgColor(Vector4 color)
    {
        BgRed = color.X;
        BgGreen = color.Y;
        BgBlue = color.Z;
        BgAlpha = color.W;
    }

    public void SetTextColor(Vector4 color)
    {
        TextRed = color.X;
        TextGreen = color.Y;
        TextBlue = color.Z;
        TextAlpha = color.W;
    }

    public void SetTextHoverColor(Vector4 color)
    {
        TextHoverRed = color.X;
        TextHoverGreen = color.Y;
        TextHoverBlue = color.Z;
        TextHoverAlpha = color.W;
    }

    public void SetButtonBgColor(Vector4 color)
    {
        ButtonBgRed = color.X;
        ButtonBgGreen = color.Y;
        ButtonBgBlue = color.Z;
        ButtonBgAlpha = color.W;
    }

    public void SetButtonBgHoverColor(Vector4 color)
    {
        ButtonBgHoverRed = color.X;
        ButtonBgHoverGreen = color.Y;
        ButtonBgHoverBlue = color.Z;
        ButtonBgHoverAlpha = color.W;
    }

    public void EnsureDefaults()
    {
        if (Padding < 0f)
            Padding = 0f;

        if (Width <= 0f)
            Width = 100f;
    }

    public static ContextMenuStyleConfig CreateDefault() => new();
}


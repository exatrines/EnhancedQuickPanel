namespace EnhancedQuickPanel.Models;

/// <summary>Colors for the page bar buttons.</summary>
public sealed class PageBarButtonStyleConfig
{
    public float BgRed { get; set; } = 0.2f;

    public float BgGreen { get; set; } = 0.2f;

    public float BgBlue { get; set; } = 0.2f;

    public float BgAlpha { get; set; } = 1f;

    public float BgHoverRed { get; set; } = 0.31f;

    public float BgHoverGreen { get; set; } = 0.31f;

    public float BgHoverBlue { get; set; } = 0.31f;

    public float BgHoverAlpha { get; set; } = 1f;

    public float TextRed { get; set; } = 1f;

    public float TextGreen { get; set; } = 1f;

    public float TextBlue { get; set; } = 1f;

    public float TextAlpha { get; set; } = 1f;

    public float TextHoverRed { get; set; } = 1f;

    public float TextHoverGreen { get; set; } = 1f;

    public float TextHoverBlue { get; set; } = 1f;

    public float TextHoverAlpha { get; set; } = 1f;

    public Vector4 BgColor => new(BgRed, BgGreen, BgBlue, BgAlpha);

    public Vector4 BgHoverColor => new(BgHoverRed, BgHoverGreen, BgHoverBlue, BgHoverAlpha);

    public Vector4 TextColor => new(TextRed, TextGreen, TextBlue, TextAlpha);

    public Vector4 TextHoverColor => new(TextHoverRed, TextHoverGreen, TextHoverBlue, TextHoverAlpha);

    public void SetBgColor(Vector4 color)
    {
        BgRed = color.X;
        BgGreen = color.Y;
        BgBlue = color.Z;
        BgAlpha = color.W;
    }

    public void SetBgHoverColor(Vector4 color)
    {
        BgHoverRed = color.X;
        BgHoverGreen = color.Y;
        BgHoverBlue = color.Z;
        BgHoverAlpha = color.W;
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

    public static PageBarButtonStyleConfig CreateDefault() => new();
}


namespace EnhancedQuickPanel.Models;

/// <summary>Colors for shared panel UI elements such as the page and pen buttons and edit text.</summary>
public sealed class PanelUiStyleConfig
{
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

    public float FieldBgRed { get; set; } = 0.2f;

    public float FieldBgGreen { get; set; } = 0.2f;

    public float FieldBgBlue { get; set; } = 0.2f;

    public float FieldBgAlpha { get; set; } = 1f;

    public float PlaceholderTextRed { get; set; } = 0.6f;

    public float PlaceholderTextGreen { get; set; } = 0.6f;

    public float PlaceholderTextBlue { get; set; } = 0.6f;

    public float PlaceholderTextAlpha { get; set; } = 1f;

    public float DropdownBgRed { get; set; } = 0.2f;

    public float DropdownBgGreen { get; set; } = 0.2f;

    public float DropdownBgBlue { get; set; } = 0.2f;

    public float DropdownBgAlpha { get; set; } = 1f;

    public Vector4 TextColor => new(TextRed, TextGreen, TextBlue, TextAlpha);

    public Vector4 TextHoverColor => new(TextHoverRed, TextHoverGreen, TextHoverBlue, TextHoverAlpha);

    public Vector4 ButtonBgColor => new(ButtonBgRed, ButtonBgGreen, ButtonBgBlue, ButtonBgAlpha);

    public Vector4 ButtonBgHoverColor =>
        new(ButtonBgHoverRed, ButtonBgHoverGreen, ButtonBgHoverBlue, ButtonBgHoverAlpha);

    public Vector4 FieldBgColor => new(FieldBgRed, FieldBgGreen, FieldBgBlue, FieldBgAlpha);

    public Vector4 PlaceholderTextColor =>
        new(PlaceholderTextRed, PlaceholderTextGreen, PlaceholderTextBlue, PlaceholderTextAlpha);

    public Vector4 DropdownBgColor =>
        new(DropdownBgRed, DropdownBgGreen, DropdownBgBlue, DropdownBgAlpha);

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

    public void SetFieldBgColor(Vector4 color)
    {
        FieldBgRed = color.X;
        FieldBgGreen = color.Y;
        FieldBgBlue = color.Z;
        FieldBgAlpha = color.W;
    }

    public void SetPlaceholderTextColor(Vector4 color)
    {
        PlaceholderTextRed = color.X;
        PlaceholderTextGreen = color.Y;
        PlaceholderTextBlue = color.Z;
        PlaceholderTextAlpha = color.W;
    }

    public void SetDropdownBgColor(Vector4 color)
    {
        DropdownBgRed = color.X;
        DropdownBgGreen = color.Y;
        DropdownBgBlue = color.Z;
        DropdownBgAlpha = color.W;
    }

    public void EnsureDefaults()
    {
        if (PlaceholderTextRed == 0f && PlaceholderTextGreen == 0f
            && PlaceholderTextBlue == 0f && PlaceholderTextAlpha == 0f)
            SetPlaceholderTextColor(new Vector4(0.6f, 0.6f, 0.6f, 1f));

        if (DropdownBgRed == 0f && DropdownBgGreen == 0f
            && DropdownBgBlue == 0f && DropdownBgAlpha == 0f)
            SetDropdownBgColor(FieldBgColor);
    }

    public static PanelUiStyleConfig CreateDefault() => new();

    public static PanelUiStyleConfig FromPageBarStyle(PageBarButtonStyleConfig pageBar)
    {
        var style = new PanelUiStyleConfig();
        style.SetTextColor(pageBar.TextColor);
        style.SetTextHoverColor(pageBar.TextHoverColor);
        style.SetButtonBgColor(pageBar.BgColor);
        style.SetButtonBgHoverColor(pageBar.BgHoverColor);
        return style;
    }
}


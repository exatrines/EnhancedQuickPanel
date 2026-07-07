namespace EnhancedQuickPanel.Services;

/// <summary>Overlay label values computed for a slot.</summary>
internal readonly record struct SlotOverlayInfo(
    bool ShowQuantity,
    int Quantity,
    bool IsGrayedOut,
    bool ShowMacroIndicator,
    bool ShowActionCharges = false,
    int ActionCharges = 0)
{
    public static SlotOverlayInfo None => default;
}


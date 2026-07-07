using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Computes overlay label font sizes relative to slot size.</summary>
internal static class SlotOverlayFontSizeResolver
{
    private const float ReferenceSlotSize = 48f;
    private const float MinScale = 0.65f;
    private const float MaxScale = 1.15f;

    public static float ResolveBaseFontSize(float slotSizePixels)
    {
        var scale = Math.Clamp(slotSizePixels / ReferenceSlotSize, MinScale, MaxScale);
        return ImGui.GetFontSize() * scale;
    }

    public static float ResolveFontSize(float slotSizePixels, OverlayLabelStyleConfig style) =>
        style.ScaleFontSize(ResolveBaseFontSize(slotSizePixels));
}


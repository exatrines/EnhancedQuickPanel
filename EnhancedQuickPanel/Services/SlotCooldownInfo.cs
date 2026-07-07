namespace EnhancedQuickPanel.Services;

/// <summary>Remaining cooldown fraction and seconds for a slot.</summary>
internal readonly record struct SlotCooldownInfo(float RemainingFraction, float SecondsRemaining)
{
    public float ElapsedFraction => Math.Clamp(1f - RemainingFraction, 0f, 1f);

    public bool IsActive => RemainingFraction > 0f || SecondsRemaining > 0.05f;

    public static SlotCooldownInfo None => default;
}


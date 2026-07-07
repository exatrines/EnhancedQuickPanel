namespace EnhancedQuickPanel.Services.MacroIcon;

/// <summary>A resolved macro icon: category, name, icon ID, and row ID.</summary>
internal readonly record struct MacroIconEntry(
    MacroIconCategory Category,
    string Name,
    uint IconId,
    uint RowId,
    string? Detail = null);


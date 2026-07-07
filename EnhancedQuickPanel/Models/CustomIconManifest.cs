namespace EnhancedQuickPanel.Models;

/// <summary>Serializable list of custom icons persisted to disk.</summary>
public sealed class CustomIconManifest
{
    public List<CustomIconEntry> Icons { get; set; } = [];
}


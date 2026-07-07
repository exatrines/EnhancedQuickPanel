namespace EnhancedQuickPanel.Models;

/// <summary>Which entries are shown in the overlay's right-click context menu.</summary>
public sealed class ContextMenuItemsConfig
{
    public bool? ShowEdit { get; set; }

    public bool? ShowSettings { get; set; }

    public bool? ShowExportPage { get; set; }

    public bool? ShowImportPage { get; set; }

    public bool? ShowImportNative { get; set; }

    public bool? ShowClose { get; set; }

    public bool IsEditVisible => ShowEdit ?? true;

    public bool IsSettingsVisible => ShowSettings ?? true;

    public bool IsExportPageVisible => ShowExportPage ?? true;

    public bool IsImportPageVisible => ShowImportPage ?? true;

    public bool IsImportNativeVisible => ShowImportNative ?? true;

    public bool IsCloseVisible => ShowClose ?? true;

    public bool HasVisibleItems =>
        IsEditVisible
        || IsSettingsVisible
        || IsExportPageVisible
        || IsImportPageVisible
        || IsImportNativeVisible
        || IsCloseVisible;

    public static ContextMenuItemsConfig CreateDefault() => new();
}

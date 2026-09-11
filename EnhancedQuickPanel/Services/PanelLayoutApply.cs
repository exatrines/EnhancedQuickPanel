using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Applies a plugin-wide panel layout to every page.</summary>
internal static class PanelLayoutApply
{
    public static bool TryApply(int blockColumns, int blockRows, out string error)
    {
        error = string.Empty;
        Config.EnsureDefaults();

        blockColumns = PanelLayout.ClampBlocks(blockColumns);
        blockRows = PanelLayout.ClampBlocks(blockRows);

        var sourceColumns = Config.GridColumns;
        var sourceRows = Config.GridRows;
        var destColumns = PanelLayout.Columns(blockColumns);
        var destRows = PanelLayout.Rows(blockRows);

        if (destColumns == sourceColumns && destRows == sourceRows)
            return true;

        if ((destColumns < sourceColumns || destRows < sourceRows)
            && PagesHaveConfiguredOutside(sourceColumns, sourceRows, destColumns, destRows))
        {
            error = T("config.layout.shrinkBlocked");
            return false;
        }

        foreach (var page in Config.Pages)
        {
            page.Slots = PanelLayout.RemapKeepPositions(
                page.Slots,
                sourceColumns,
                sourceRows,
                destColumns,
                destRows);
        }

        Config.LayoutBlockColumns = blockColumns;
        Config.LayoutBlockRows = blockRows;
        Config.EnsureDefaults();
        Config.Save();
        return true;
    }

    private static bool PagesHaveConfiguredOutside(
        int sourceColumns,
        int sourceRows,
        int destColumns,
        int destRows)
    {
        foreach (var page in Config.Pages)
        {
            if (PanelLayout.HasConfiguredOutside(
                    page.Slots,
                    sourceColumns,
                    sourceRows,
                    destColumns,
                    destRows))
                return true;
        }

        return false;
    }
}

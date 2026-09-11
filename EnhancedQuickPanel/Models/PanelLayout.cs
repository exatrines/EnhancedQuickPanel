namespace EnhancedQuickPanel.Models;

/// <summary>Block-based panel sizing and slot remapping. One block is the native 5×5 grid.</summary>
public static class PanelLayout
{
    public const int BlockSize = 5;
    public const int MinBlocks = 1;
    public const int MaxBlocks = 2;

    public static int ClampBlocks(int value) =>
        Math.Clamp(value, MinBlocks, MaxBlocks);

    public static int Columns(int blockColumns) =>
        BlockSize * ClampBlocks(blockColumns);

    public static int Rows(int blockRows) =>
        BlockSize * ClampBlocks(blockRows);

    public static bool CanFit(int sourceColumns, int sourceRows, int destColumns, int destRows) =>
        sourceColumns > 0
        && sourceRows > 0
        && sourceColumns <= destColumns
        && sourceRows <= destRows;

    public static float ComputeSpan(int cells, float slotSize, float padding) =>
        cells * slotSize + Math.Max(0, cells - 1) * padding;

    public static List<PanelSlot> CreateSlots(int count)
    {
        var slots = new List<PanelSlot>(Math.Max(0, count));
        for (var i = 0; i < count; i++)
            slots.Add(new PanelSlot());
        return slots;
    }

    public static bool HasConfiguredOutside(
        IReadOnlyList<PanelSlot> source,
        int sourceColumns,
        int sourceRows,
        int destColumns,
        int destRows)
    {
        for (var row = 0; row < sourceRows; row++)
        {
            for (var col = 0; col < sourceColumns; col++)
            {
                if (row < destRows && col < destColumns)
                    continue;

                if (Get(source, row * sourceColumns + col).IsConfigured)
                    return true;
            }
        }

        return false;
    }

    public static List<PanelSlot> RemapKeepPositions(
        IReadOnlyList<PanelSlot> source,
        int sourceColumns,
        int sourceRows,
        int destColumns,
        int destRows)
    {
        var dest = CreateSlots(destColumns * destRows);
        var rows = Math.Min(sourceRows, destRows);
        var cols = Math.Min(sourceColumns, destColumns);
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
                dest[row * destColumns + col] = Get(source, row * sourceColumns + col);
        }

        return dest;
    }

    public static void PasteTopLeft(
        List<PanelSlot> dest,
        int destColumns,
        int destRows,
        IReadOnlyList<PanelSlot> source,
        int sourceColumns,
        int sourceRows)
    {
        for (var row = 0; row < sourceRows; row++)
        {
            for (var col = 0; col < sourceColumns; col++)
                dest[row * destColumns + col] = Get(source, row * sourceColumns + col);
        }
    }

    public static PanelSlot Get(IReadOnlyList<PanelSlot> source, int index) =>
        index >= 0 && index < source.Count ? source[index] : new PanelSlot();
}

namespace EnhancedQuickPanel.Services.MacroIcon;

/// <summary>Static catalog of macro-icon row IDs offered by the icon picker.</summary>
internal static class MacroIconCatalogIcons
{
    private static readonly Lazy<IReadOnlyList<uint>> IconIds = new(BuildIconIds);

    private static readonly Lazy<HashSet<uint>> IconIdSet = new(() => IconIds.Value.ToHashSet());

    internal static IReadOnlyList<uint> AllIconIds => IconIds.Value;

    internal static bool Contains(uint iconId) => IconIdSet.Value.Contains(iconId);

    internal static IEnumerable<MacroIconEntry> BuildEntries()
    {
        foreach (var iconId in IconIds.Value)
            yield return new MacroIconEntry(MacroIconCategory.MacroIcon, iconId.ToString(), iconId, iconId);
    }

    private static List<uint> BuildIconIds()
    {
        var ids = new List<uint>();
        Add(ids, 66001);
        Add(ids, 66101);
        AddRange(ids, 66102, 66105);
        AddRange(ids, 66121, 66125);
        AddRange(ids, 66141, 66145);
        AddRange(ids, 66021, 66023);
        AddRange(ids, 66041, 66043);
        AddRange(ids, 66031, 66033);
        AddRange(ids, 66051, 66053);
        AddRange(ids, 66071, 66073);
        Add(ids, 1513, 1563, 1613, 1662, 1713, 1762, 1813, 1862);
        AddRange(ids, 66081, 66083);
        AddRange(ids, 66161, 66171);
        AddRange(ids, 66181, 66191);
        AddRange(ids, 66301, 66304);
        AddRange(ids, 66332, 66335);
        AddRange(ids, 66305, 66331);
        Add(ids, 66336, 66340, 66339, 66338, 66341, 66337);
        Add(ids, 20007, 20009, 20010, 20012, 20011, 20008);
        return ids;
    }

    private static void AddRange(List<uint> ids, uint start, uint end)
    {
        for (var id = start; id <= end; id++)
            Add(ids, id);
    }

    private static void Add(List<uint> ids, params uint[] values)
    {
        foreach (var value in values)
        {
            if (!ids.Contains(value))
                ids.Add(value);
        }
    }
}


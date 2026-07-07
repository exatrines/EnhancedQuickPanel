namespace EnhancedQuickPanel.Services.CustomIcons;

/// <summary>Encodes and decodes the ID range reserved for custom icons.</summary>
internal static class CustomIconIds
{
    public const uint Base = 10_000_000;
    public const string DisplayPrefix = "EQP#";

    public static bool IsCustom(uint iconId) => iconId > Base;

    public static uint FromFileStem(string fileStem)
    {
        if (!CustomIconFileNames.IsValidStem(fileStem))
            return 0;

        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in fileStem.ToLowerInvariant())
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return Base + (hash % 990_000u) + 1u;
        }
    }

    public static string FormatDisplayId(string fileName) =>
        $"{DisplayPrefix}{CustomIconFileNames.GetStem(fileName)}";

    public static string FormatDisplayId(uint iconId, string fileName) =>
        IsCustom(iconId) ? FormatDisplayId(fileName) : iconId.ToString();
}


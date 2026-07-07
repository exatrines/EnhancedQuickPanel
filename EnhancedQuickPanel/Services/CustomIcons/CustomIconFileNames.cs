using System.Text;

namespace EnhancedQuickPanel.Services.CustomIcons;

/// <summary>Builds and validates file names for stored custom icons.</summary>
internal static class CustomIconFileNames
{
    public static string GetStem(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName);

    public static bool IsValidStem(string stem) =>
        !string.IsNullOrWhiteSpace(stem)
        && stem.All(ch => !IsInvalidFileNameChar(ch));

    public static bool IsImportableFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var extension = Path.GetExtension(fileName);
        if (!IsSupportedImageExtension(extension))
            return false;

        return IsValidStem(GetStem(fileName));
    }

    public static string NormalizeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var normalizedStem = NormalizeStem(GetStem(fileName));
        return $"{normalizedStem}{extension}";
    }

    public static string NormalizeStemForNewFile(string source)
    {
        var normalized = NormalizeStem(source);
        return string.IsNullOrWhiteSpace(normalized) ? "icon" : normalized;
    }

    public static bool IsSupportedImageExtension(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStem(string stem)
    {
        var builder = new StringBuilder(stem.Length);
        foreach (var ch in stem)
        {
            if (ch == ' ')
            {
                builder.Append('_');
                continue;
            }

            if (IsInvalidFileNameChar(ch))
                continue;

            builder.Append(ch);
        }

        return builder.ToString().Trim('_');
    }

    private static bool IsInvalidFileNameChar(char ch) =>
        ch is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|';
}


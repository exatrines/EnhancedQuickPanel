namespace EnhancedQuickPanel.Services.MacroIcon;

/// <summary>Parsed name and category from a /micon command.</summary>
internal readonly record struct MacroIconCommandParseResult(
    string Name,
    MacroIconCategory Category);

/// <summary>Parses /micon and /macroicon commands into a name and category.</summary>
internal static class MacroIconCommandParser
{
    private static readonly Dictionary<string, MacroIconCategory> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["action"] = MacroIconCategory.Action,
        ["macroicon"] = MacroIconCategory.MacroIcon,
        ["emote"] = MacroIconCategory.Emote,
        ["buddy"] = MacroIconCategory.Buddy,
        ["pet"] = MacroIconCategory.Pet,
        ["minion"] = MacroIconCategory.Minion,
        ["mount"] = MacroIconCategory.Mount,
        ["item"] = MacroIconCategory.Item,
        ["marking"] = MacroIconCategory.Marking,
        ["enemysign"] = MacroIconCategory.Marking,
        ["fieldmarking"] = MacroIconCategory.FieldMarking,
        ["waymark"] = MacroIconCategory.FieldMarking,
        ["ウェイマーク"] = MacroIconCategory.FieldMarking,
        ["フィールドマーキング"] = MacroIconCategory.FieldMarking,
        ["フィールドマーカー"] = MacroIconCategory.FieldMarking,
        ["classjob"] = MacroIconCategory.ClassJob,
        ["quickchat"] = MacroIconCategory.QuickChat,
        ["アクション"] = MacroIconCategory.Action,
        ["アイテム"] = MacroIconCategory.Item,
        ["エモート"] = MacroIconCategory.Emote,
        ["マウント"] = MacroIconCategory.Mount,
        ["ミニオン"] = MacroIconCategory.Minion,
        ["マーカー"] = MacroIconCategory.Marking,
        ["マーキング"] = MacroIconCategory.Marking,
        ["クラス・ジョブ"] = MacroIconCategory.ClassJob,
        ["クイックチャット"] = MacroIconCategory.QuickChat,
        ["マクロアイコン"] = MacroIconCategory.MacroIcon,
        ["バディ"] = MacroIconCategory.Buddy,
        ["ペット"] = MacroIconCategory.Pet,
    };

    internal static bool TryGetCategory(string keyword, out MacroIconCategory category) =>
        Keywords.TryGetValue(keyword.Trim(), out category);

    internal static bool TryParse(string input, out MacroIconCommandParseResult result)
    {
        result = default;
        var text = input.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.StartsWith("/macroicon", StringComparison.OrdinalIgnoreCase))
            text = text["/macroicon".Length..].TrimStart();
        else if (text.StartsWith("/micon", StringComparison.OrdinalIgnoreCase))
            text = text["/micon".Length..].TrimStart();
        else
            return false;

        if (!TryParseIconName(ref text, out var name))
            return false;

        var category = MacroIconCategory.Action;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var categoryToken = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (!Keywords.TryGetValue(categoryToken, out category))
                return false;
        }

        result = new MacroIconCommandParseResult(name, category);
        return true;
    }

    internal static bool TryParseIconId(string input, out uint iconId)
    {
        iconId = 0;
        var text = input.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.StartsWith("/macroicon", StringComparison.OrdinalIgnoreCase))
            text = text["/macroicon".Length..].TrimStart();
        else if (text.StartsWith("/micon", StringComparison.OrdinalIgnoreCase))
            text = text["/micon".Length..].TrimStart();
        else
            return false;

        if (text.StartsWith('#'))
            text = text[1..].TrimStart();
        else if (text.StartsWith("id ", StringComparison.OrdinalIgnoreCase))
            text = text[3..].TrimStart();
        else
            return false;

        var token = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token != null && uint.TryParse(token, out iconId) && iconId > 0;
    }

    private static bool TryParseIconName(ref string text, out string name)
    {
        text = text.TrimStart();
        name = string.Empty;
        if (text.Length == 0)
            return false;

        if (text[0] == '"')
        {
            var end = 1;
            var buffer = new System.Text.StringBuilder();
            while (end < text.Length)
            {
                if (text[end] == '\\' && end + 1 < text.Length)
                {
                    buffer.Append(text[end + 1]);
                    end += 2;
                    continue;
                }

                if (text[end] == '"')
                    break;

                buffer.Append(text[end]);
                end++;
            }

            if (end >= text.Length || text[end] != '"')
                return false;

            name = buffer.ToString();
            text = text[(end + 1)..].TrimStart();
            return name.Length > 0;
        }

        var spaceIndex = text.IndexOf(' ');
        if (spaceIndex < 0)
        {
            name = text;
            text = string.Empty;
        }
        else
        {
            name = text[..spaceIndex];
            text = text[spaceIndex..].TrimStart();
        }

        return name.Length > 0;
    }
}


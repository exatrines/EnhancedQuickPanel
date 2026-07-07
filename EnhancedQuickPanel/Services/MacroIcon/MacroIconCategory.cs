namespace EnhancedQuickPanel.Services.MacroIcon;

/// <summary>Categories of icons that a /micon command can reference.</summary>
internal enum MacroIconCategory
{
    MacroIcon,
    Action,
    Emote,
    Buddy,
    Pet,
    Minion,
    Mount,
    Item,
    Marking,
    FieldMarking,
    ClassJob,
    QuickChat,
}

/// <summary>Helper extensions for MacroIconCategory.</summary>
internal static class MacroIconCategoryExtensions
{
    internal static readonly MacroIconCategory[] PickerCategories =
    [
        MacroIconCategory.MacroIcon,
        MacroIconCategory.Marking,
        MacroIconCategory.FieldMarking,
        MacroIconCategory.ClassJob,
        MacroIconCategory.Emote,
        MacroIconCategory.Action,
        MacroIconCategory.Buddy,
        MacroIconCategory.Pet,
        MacroIconCategory.Minion,
        MacroIconCategory.Mount,
        MacroIconCategory.Item,
        MacroIconCategory.QuickChat,
    ];

    internal static string CommandKeyword(this MacroIconCategory category) => category switch
    {
        MacroIconCategory.Action => "action",
        MacroIconCategory.MacroIcon => "macroicon",
        MacroIconCategory.Emote => "emote",
        MacroIconCategory.Buddy => "buddy",
        MacroIconCategory.Pet => "pet",
        MacroIconCategory.Minion => "minion",
        MacroIconCategory.Mount => "mount",
        MacroIconCategory.Item => "item",
        MacroIconCategory.Marking => "marking",
        MacroIconCategory.FieldMarking => "fieldmarking",
        MacroIconCategory.ClassJob => "classjob",
        MacroIconCategory.QuickChat => "quickchat",
        _ => "action",
    };

    internal static bool OmitCategoryInCommand(this MacroIconCategory category) =>
        category == MacroIconCategory.Action;

    internal static string DisplayName(this MacroIconCategory category) => category switch
    {
        MacroIconCategory.Action => T("macroIcon.category.action"),
        MacroIconCategory.MacroIcon => T("macroIcon.category.macroIcon"),
        MacroIconCategory.Emote => T("macroIcon.category.emote"),
        MacroIconCategory.Buddy => T("macroIcon.category.buddy"),
        MacroIconCategory.Pet => T("macroIcon.category.pet"),
        MacroIconCategory.Minion => T("macroIcon.category.minion"),
        MacroIconCategory.Mount => T("macroIcon.category.mount"),
        MacroIconCategory.Item => T("macroIcon.category.item"),
        MacroIconCategory.Marking => T("macroIcon.category.marking"),
        MacroIconCategory.FieldMarking => T("macroIcon.category.fieldMarking"),
        MacroIconCategory.ClassJob => T("macroIcon.category.classJob"),
        MacroIconCategory.QuickChat => T("macroIcon.category.quickChat"),
        _ => category.ToString(),
    };
}


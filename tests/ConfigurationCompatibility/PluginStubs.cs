namespace EnhancedQuickPanel
{
    internal static class I18n
    {
        public static string T(string key) => key;

        public static string T(string key, params object[] args) =>
            args.Length == 0 ? key : string.Format(key, args);
    }
}

namespace EnhancedQuickPanel.Services
{
    internal static class PanelStylePresets
    {
        public static void ApplyDefault(Configuration config)
        {
        }
    }
}

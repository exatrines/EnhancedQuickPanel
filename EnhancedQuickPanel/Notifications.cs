using Dalamud.Interface.ImGuiNotification;

namespace EnhancedQuickPanel;

/// <summary>Shows Dalamud notifications.</summary>
internal static class Notifications
{
    public static void Success(string text) => Show(text, NotificationType.Success);

    public static void Warning(string text) => Show(text, NotificationType.Warning);

    public static void Error(string text) => Show(text, NotificationType.Error);

    private static void Show(string text, NotificationType type)
    {
        _ = PluginServices.Framework.RunOnTick(() =>
        {
            PluginServices.NotificationManager.AddNotification(new Notification
            {
                Content = text,
                Title = "Enhanced Quick Panel",
                Type = type,
            });
        });
    }
}

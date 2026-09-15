using System.Reflection;

namespace EnhancedQuickPanel;

/// <summary>Reads Dalamud internal services used by plugin shortcuts.</summary>
internal static class DalamudReflection
{
    private static readonly Dictionary<string, object> Cached = new(StringComparer.Ordinal);

    public static object GetService(string serviceFullName)
    {
        if (Cached.TryGetValue(serviceFullName, out var existing))
            return existing;

        var assembly = PluginServices.PluginInterface.GetType().Assembly;
        var serviceType = assembly.GetType("Dalamud.Service`1", throwOnError: true)!
            .MakeGenericType(assembly.GetType(serviceFullName, throwOnError: true)!);
        var instance = serviceType.GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, [], null)!;
        Cached[serviceFullName] = instance;
        return instance;
    }

    public static object GetPluginManager() =>
        GetService("Dalamud.Plugin.Internal.PluginManager");
}

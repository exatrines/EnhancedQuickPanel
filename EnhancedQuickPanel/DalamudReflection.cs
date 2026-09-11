using System.Reflection;

namespace EnhancedQuickPanel;

/// <summary>Reads Dalamud internal services used by plugin shortcuts.</summary>
internal static class DalamudReflection
{
    public static object GetService(string serviceFullName)
    {
        var assembly = PluginServices.PluginInterface.GetType().Assembly;
        var serviceType = assembly.GetType("Dalamud.Service`1", throwOnError: true)!
            .MakeGenericType(assembly.GetType(serviceFullName, throwOnError: true)!);
        return serviceType.GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, [], null)!;
    }

    public static object GetPluginManager() =>
        GetService("Dalamud.Plugin.Internal.PluginManager");
}

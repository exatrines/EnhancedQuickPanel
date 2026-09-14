using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace EnhancedQuickPanel.Services;

/// <summary>Loads or unloads an installed plugin through Dalamud internals.</summary>
internal static class PluginLifecycleToggle
{
    private static bool apiUnavailable;
    private static readonly ConcurrentDictionary<string, byte> Running = new();

    public static bool IsBusy(string internalName, string workingPluginId)
    {
        var key = BusyKey(internalName, workingPluginId);
        return !string.IsNullOrEmpty(key)
            && (Running.ContainsKey(key)
                || (!string.IsNullOrEmpty(internalName) && Running.ContainsKey(internalName)));
    }

    public static bool CanToggle(string internalName, string workingPluginId, out string? reason) =>
        TryCreateJob(internalName, workingPluginId, out _, out reason);

    public static void Request(string internalName, string workingPluginId)
    {
        if (PluginLifetime.IsStopping)
            return;

        if (!TryCreateJob(internalName, workingPluginId, out var job, out var reason))
        {
            NotifyImmediate(reason);
            return;
        }

        if (!Running.TryAdd(job.BusyKey, 0))
            return;
        if (!string.Equals(job.BusyKey, job.InternalName, StringComparison.Ordinal))
            Running.TryAdd(job.InternalName, 0);

        _ = Task.Run(() => RunJobAsync(job));
    }

    private static async Task RunJobAsync(ToggleJob job)
    {
        try
        {
            await ToggleJobAsync(job);
        }
        catch (Exception ex)
        {
            MarkApiUnavailable(ex);
            PluginLifetime.TryLogError($"Could not toggle plugin {job.InternalName}: {ex}");
            NotifyIfRunning(job.FailedMessage);
        }
        finally
        {
            Running.TryRemove(job.BusyKey, out _);
            Running.TryRemove(job.InternalName, out _);
        }
    }

    private static async Task ToggleJobAsync(ToggleJob job)
    {
        var state = job.LocalType.GetProperty("State")?.GetValue(job.Local)?.ToString();
        if (state is "Loading" or "Unloading")
            return;

        if (!await PersistWantedAsync(job, job.WantEnabled))
        {
            NotifyIfRunning(job.FailedMessage);
            return;
        }

        try
        {
            if (job.WantEnabled)
                await InvokeLoadAsync(job.Local, job.LocalType);
            else
                await InvokeUnloadAsync(job.Local, job.LocalType);
        }
        catch (Exception ex)
        {
            MarkApiUnavailable(ex);
            PluginLifetime.TryLogError($"Could not toggle plugin {job.InternalName}: {ex}");
            if (!await PersistWantedAsync(job, job.PreviousWanted))
            {
                PluginLifetime.TryLogError(
                    $"Could not restore plugin profile state for {job.InternalName} to {job.PreviousWanted}.");
            }

            NotifyIfRunning(job.FailedMessage);
        }
    }

    private static bool TryCreateJob(string internalName, string workingPluginId, out ToggleJob job, out string? reason)
    {
        job = default;
        reason = null;
        if (PluginLifetime.IsStopping || string.IsNullOrWhiteSpace(internalName) || apiUnavailable)
        {
            reason = T("shortcut.toggleFailed");
            return false;
        }

        if (PluginShortcuts.IsSelf(internalName))
        {
            reason = T("shortcut.selfToggle");
            return false;
        }

        if (IsBusy(internalName, workingPluginId))
        {
            reason = T("shortcut.toggleBusy");
            return false;
        }

        var local = PluginShortcuts.FindLocal(internalName, workingPluginId);
        if (local == null)
        {
            reason = T("shortcut.unavailable");
            return false;
        }

        var type = PluginShortcuts.LocalType(local);
        if (!TryGetOwningProfile(local, type, out var profile, out reason))
            return false;

        var isLoaded = PluginShortcuts.IsLocalLoaded(local);
        if (!isLoaded && PluginShortcuts.HasOtherLoadedInstance(local))
        {
            reason = T("shortcut.toggleAlreadyEnabled");
            return false;
        }
        var workingId = type.GetProperty("EffectiveWorkingPluginId")?.GetValue(local);
        var persistedName = PluginShortcuts.ReadLocalInternalName(local);
        var previousWanted = ReadWantsPlugin(profile, workingId);
        if (workingId == null || string.IsNullOrEmpty(persistedName) || previousWanted == null)
        {
            reason = T("shortcut.toggleFailed");
            return false;
        }

        var busyKey = BusyKey(internalName, workingId is Guid guid && guid != Guid.Empty ? guid.ToString("D") : workingPluginId);
        job = new ToggleJob(
            internalName,
            busyKey,
            local,
            type,
            profile,
            workingId,
            persistedName,
            previousWanted.Value,
            !isLoaded,
            T("shortcut.toggleFailed"));
        reason = null;
        return true;
    }

    private static string BusyKey(string internalName, string workingPluginId) =>
        !string.IsNullOrWhiteSpace(workingPluginId) ? workingPluginId.ToLowerInvariant() : internalName;

    private static async Task InvokeLoadAsync(object local, Type type)
    {
        var method = type.GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(type.Name, "LoadAsync");
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            throw new MissingMethodException(type.Name, "LoadAsync(PluginLoadReason)");
        var reason = Enum.Parse(parameters[0].ParameterType, "Installer");
        object?[] args = parameters.Length switch
        {
            1 => [reason],
            2 => [reason, false],
            _ => [reason, false, CancellationToken.None],
        };
        if (method.Invoke(local, args) is not Task task)
            throw new InvalidOperationException("LoadAsync did not return a task.");
        await task;
    }

    private static async Task InvokeUnloadAsync(object local, Type type)
    {
        var method = type.GetMethod("UnloadAsync", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(type.Name, "UnloadAsync");
        var parameters = method.GetParameters();
        object?[] args = parameters.Length == 0
            ? []
            : [Enum.Parse(parameters[0].ParameterType, "WaitBeforeDispose")];
        if (method.Invoke(local, args) is not Task task)
            throw new InvalidOperationException("UnloadAsync did not return a task.");
        await task;
    }

    private static async Task<bool> PersistWantedAsync(ToggleJob job, bool enabled)
    {
        try
        {
            var method = job.Profile.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "AddOrUpdateAsync" && m.GetParameters().Length >= 3);
            if (method == null)
                return false;

            object?[] args = method.GetParameters().Length >= 4
                ? [job.WorkingId, job.PersistedName, enabled, false]
                : [job.WorkingId, job.PersistedName, enabled];
            if (method.Invoke(job.Profile, args) is not Task task)
                return false;
            await task;
            return true;
        }
        catch (Exception ex)
        {
            PluginLifetime.TryLogWarning($"Could not persist plugin enabled state: {ex.Message}");
            return false;
        }
    }

    private static bool? ReadWantsPlugin(object profile, object? workingId)
    {
        if (workingId == null)
            return null;
        var result = profile.GetType().GetMethod("WantsPlugin")?.Invoke(profile, [workingId]);
        return result is bool wanted ? wanted : null;
    }

    private static bool TryGetOwningProfile(object local, Type type, out object profile, out string? reason)
    {
        profile = null!;
        reason = T("shortcut.toggleFailed");

        var profileManager = GetProfileManager();
        if (profileManager == null)
            return false;

        if (profileManager.GetType().GetProperty("IsBusy")?.GetValue(profileManager) is true)
        {
            reason = T("shortcut.toggleBusy");
            return false;
        }

        var workingId = type.GetProperty("EffectiveWorkingPluginId")?.GetValue(local);
        if (workingId == null)
            return false;

        var profilesValue = profileManager.GetType().GetProperty("Profiles")?.GetValue(profileManager);
        if (profilesValue is not IEnumerable profiles)
            return false;

        var matches = new List<object>();
        foreach (var candidate in profiles)
        {
            if (candidate == null)
                continue;
            var wants = candidate.GetType().GetMethod("WantsPlugin")?.Invoke(candidate, [workingId]);
            if (wants != null)
                matches.Add(candidate);
        }

        if (matches.Count != 1)
        {
            reason = T("shortcut.toggleMultipleProfiles");
            return false;
        }

        profile = matches[0];
        var profileType = profile.GetType();
        var name = profileType.GetProperty("Name")?.GetValue(profile) as string ?? string.Empty;
        if (profileType.GetProperty("IsEnabled")?.GetValue(profile) is false)
        {
            reason = T("shortcut.toggleProfileDisabled", name);
            profile = null!;
            return false;
        }

        var check = profileType.GetMethod("CheckWantsActiveFromGameState");
        if (check != null
            && check.Invoke(profile, [PluginServices.PlayerState.ContentId]) is false)
        {
            reason = T("shortcut.toggleProfileInactive", name);
            profile = null!;
            return false;
        }

        reason = null;
        return true;
    }

    private static object? GetProfileManager()
    {
        var manager = DalamudReflection.GetPluginManager();
        return manager.GetType()
                .GetProperty("ProfileManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(manager)
            ?? manager.GetType()
                .GetField("profileManager", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(manager);
    }

    private static void NotifyImmediate(string? reason)
    {
        if (PluginLifetime.IsStopping || string.IsNullOrEmpty(reason))
            return;

        try
        {
            PluginServices.Chat.PrintError(reason);
        }
        catch
        {
        }
    }

    private static void NotifyIfRunning(string message)
    {
        if (PluginLifetime.IsStopping)
            return;

        try
        {
            _ = PluginServices.Framework.RunOnTick(() =>
            {
                if (PluginLifetime.IsStopping)
                    return;

                PluginServices.Chat.PrintError(message);
            });
        }
        catch
        {
        }
    }

    private readonly record struct ToggleJob(
        string InternalName,
        string BusyKey,
        object Local,
        Type LocalType,
        object Profile,
        object WorkingId,
        string PersistedName,
        bool PreviousWanted,
        bool WantEnabled,
        string FailedMessage);

    private static void MarkApiUnavailable(Exception ex) =>
        apiUnavailable |= ex is MissingMethodException or MissingFieldException;
}

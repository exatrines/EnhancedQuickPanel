using System.Threading;

namespace EnhancedQuickPanel;

/// <summary>Plugin run state: stop new work, cancel downloads, and gate file commits.</summary>
internal static class PluginLifetime
{
    private static readonly object Gate = new();
    private static CancellationTokenSource _cts = new();
    private static bool _stopping = true;

    public static bool IsStopping
    {
        get
        {
            lock (Gate)
                return _stopping;
        }
    }

    public static CancellationToken Token
    {
        get
        {
            lock (Gate)
                return _cts.Token;
        }
    }

    public static void Start()
    {
        lock (Gate)
        {
            ReplaceTokenSource();
            _stopping = false;
        }
    }

    public static void BeginStop()
    {
        lock (Gate)
        {
            if (_stopping)
                return;

            _stopping = true;
            Ignore(() => _cts.Cancel());
        }
    }

    public static bool TryCommitFile(Action commit)
    {
        lock (Gate)
        {
            if (_stopping)
                return false;

            commit();
            return true;
        }
    }

    public static async Task<bool> WriteAndCommitAsync(
        string destPath,
        byte[] bytes,
        CancellationToken cancellationToken,
        Action? afterMove = null)
    {
        var tempPath = destPath + ".new";
        var committed = false;
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            committed = TryCommitFile(() =>
            {
                File.Move(tempPath, destPath, overwrite: true);
                afterMove?.Invoke();
            });
            return committed;
        }
        finally
        {
            if (!committed)
                TryDeleteFile(tempPath);
        }
    }

    public static void TryLogError(string message) =>
        Ignore(() => PluginServices.Log.Error(message));

    public static void TryLogWarning(string message) =>
        Ignore(() => PluginServices.Log.Warning(message));

    public static void TryLogDebug(string message) =>
        Ignore(() => PluginServices.Log.Debug(message));

    public static void TryDeleteFile(string path) =>
        Ignore(() =>
        {
            if (File.Exists(path))
                File.Delete(path);
        });

    private static void ReplaceTokenSource()
    {
        var previous = _cts;
        _cts = new CancellationTokenSource();
        Ignore(() => previous.Cancel());
        Ignore(() => previous.Dispose());
    }

    private static void Ignore(Action action)
    {
        try
        {
            action();
        }
        catch
        {
        }
    }
}

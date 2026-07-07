using ECommons.Automation;

namespace EnhancedQuickPanel.Services;

/// <summary>Queues and executes chat/text command lines on the game thread.</summary>
internal static class TextCommandExecutor
{
    private const int LineDelayMs = 250;

    /// <summary>A queued text-command line with its scheduled execution time.</summary>
    private readonly record struct PendingLine(long ExecuteAt, string Line, int RunId);

    private static readonly List<PendingLine> Pending = [];
    private static int _runId;

    public static void Execute(string textBody)
    {
        CancelPending();

        var lines = textBody
            .Replace("\r", string.Empty)
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
            return;

        var runId = ++_runId;
        var startAt = Environment.TickCount64;

        for (var index = 0; index < lines.Length; index++)
        {
            Pending.Add(new PendingLine(
                startAt + index * LineDelayMs,
                lines[index],
                runId));
        }
    }

    public static void ProcessPending()
    {
        if (Pending.Count == 0 || !GameModuleGuard.IsClientReady)
            return;

        var now = Environment.TickCount64;

        while (Pending.Count > 0 && Pending[0].ExecuteAt <= now)
        {
            var item = Pending[0];
            Pending.RemoveAt(0);

            if (item.RunId != _runId)
                continue;

            ExecuteLine(item.Line);
        }
    }

    public static void CancelPending()
    {
        _runId++;
        Pending.Clear();
    }

    private static void ExecuteLine(string line) => MacroManager.Execute(line);
}


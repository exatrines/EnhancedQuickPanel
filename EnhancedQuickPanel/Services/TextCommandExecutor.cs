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
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        if (lines.Length == 0)
            return;

        var runId = ++_runId;
        var scheduleAt = Environment.TickCount64;

        foreach (var line in lines)
        {
            if (TryParseWait(line, out var waitMs))
            {
                scheduleAt += waitMs;
                continue;
            }

            Pending.Add(new PendingLine(scheduleAt, line, runId));
            scheduleAt += LineDelayMs;
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

    private static bool TryParseWait(string line, out int waitMs)
    {
        waitMs = 0;
        if (!line.StartsWith("/wait", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var seconds))
            return false;

        waitMs = Math.Max(0, seconds) * 1000;
        return true;
    }

    private static void ExecuteLine(string line)
    {
        GenericHelpers.Safe(() =>
        {
            if (line.StartsWith('/'))
                Chat.ExecuteCommand(line);
            else
                Chat.SendMessage(line);
        });
    }
}

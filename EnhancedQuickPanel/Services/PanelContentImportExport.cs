using System.Text.Json;
using System.Text.Json.Serialization;
using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel.Services;

/// <summary>Serializes and deserializes page slot content for clipboard sharing.</summary>
internal static class PanelContentImportExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void ExportToClipboard(PanelPage page)
    {
        try
        {
            Config.EnsureDefaults();
            ImGui.SetClipboardText(Export(page));
            Notifications.Success(T("panelContent.exportSuccess"));
        }
        catch (Exception ex)
        {
            Notifications.Error(T("panelContent.exportFailed", ex.Message));
        }
    }

    public static bool TryImportFromClipboardAsNewPage(out string error)
    {
        error = string.Empty;
        try
        {
            var json = ImGui.GetClipboardText();
            if (string.IsNullOrWhiteSpace(json))
            {
                error = T("panelContent.error.noClipboard");
                return false;
            }

            if (!TryReadSnapshot(json, out var snapshot, out error))
                return false;

            Config.EnsureDefaults();
            var page = Configuration.CreateEmptyPage(Config.SlotsPerPage, snapshot.PageName);
            if (!TryPasteSnapshot(snapshot, page, out error))
                return false;

            Config.Pages.Add(page);
            Config.Save();
            Notifications.Success(T("panelContent.importNewSuccess"));
            return true;
        }
        catch (Exception ex)
        {
            error = T("panelContent.importFailed", ex.Message);
            return false;
        }
    }

    public static string Export(PanelPage page) =>
        JsonSerializer.Serialize(PanelContentSnapshot.From(page), JsonOptions);

    private static bool TryReadSnapshot(string json, out PanelContentSnapshot snapshot, out string error)
    {
        snapshot = new PanelContentSnapshot();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = T("panelContent.error.emptyJson");
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PanelContentSnapshot>(json, JsonOptions);
            if (parsed == null)
            {
                error = T("panelContent.error.loadFailed");
                return false;
            }

            if (parsed.Version == 0)
                parsed.Version = PanelContentSnapshot.CurrentVersion;

            if (parsed.Version != PanelContentSnapshot.CurrentVersion)
            {
                error = T("panelContent.error.unsupportedVersion", parsed.Version);
                return false;
            }

            if (!parsed.TryGetDimensions(out _, out _))
            {
                error = T("panelContent.error.loadFailed");
                return false;
            }

            snapshot = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            error = T("panelContent.error.invalidJson", ex.Message);
            return false;
        }
    }

    private static bool TryPasteSnapshot(PanelContentSnapshot snapshot, PanelPage target, out string error)
    {
        error = string.Empty;
        Config.EnsureDefaults();
        target.Slots ??= [];
        while (target.Slots.Count < Config.SlotsPerPage)
            target.Slots.Add(new PanelSlot());

        if (!snapshot.TryGetDimensions(out var sourceColumns, out var sourceRows))
        {
            error = T("panelContent.error.loadFailed");
            return false;
        }

        if (!PanelLayout.CanFit(sourceColumns, sourceRows, Config.GridColumns, Config.GridRows))
        {
            error = T("panelContent.error.tooLarge", sourceColumns, sourceRows, Config.GridColumns, Config.GridRows);
            return false;
        }

        var cloned = snapshot.Slots
            .Select(PanelContentSnapshot.CloneSlot)
            .ToList();

        PanelLayout.PasteTopLeft(
            target.Slots,
            Config.GridColumns,
            Config.GridRows,
            cloned,
            sourceColumns,
            sourceRows);
        return true;
    }

    /// <summary>Serializable snapshot of a page's slots.</summary>
    internal sealed class PanelContentSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string PageName { get; set; } = string.Empty;

        public int Columns { get; set; }

        public int Rows { get; set; }

        public List<PanelSlot> Slots { get; set; } = [];

        public static PanelContentSnapshot From(PanelPage page)
        {
            Config.EnsureDefaults();
            return new PanelContentSnapshot
            {
                PageName = page.Name,
                Columns = Config.GridColumns,
                Rows = Config.GridRows,
                Slots = page.Slots
                    .Take(Config.SlotsPerPage)
                    .Select(CloneSlot)
                    .ToList(),
            };
        }

        public bool TryGetDimensions(out int columns, out int rows)
        {
            if (Columns > 0 && Rows > 0)
            {
                columns = Columns;
                rows = Rows;
                return true;
            }

            columns = Configuration.GridSize;
            rows = Configuration.GridSize;
            return true;
        }

        internal static PanelSlot CloneSlot(PanelSlot slot) =>
            JsonSerializer.Deserialize<PanelSlot>(JsonSerializer.Serialize(slot, JsonOptions), JsonOptions)
            ?? new PanelSlot();
    }
}

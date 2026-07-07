using System.Text.Json;
using System.Text.Json.Serialization;
using EnhancedQuickPanel.Models;
using ECommons.WindowsFormsReflector;

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

    public static PanelPage ClonePage(PanelPage page) =>
        JsonSerializer.Deserialize<PanelPage>(JsonSerializer.Serialize(page, JsonOptions), JsonOptions)
        ?? Configuration.CreateEmptyPage(page.Name);

    public static void ExportToClipboard(PanelPage page)
    {
        try
        {
            C.EnsureDefaults();
            Winforms.Clipboard.SetText(Export(page));
            Notify.Success(T("panelContent.exportSuccess"));
        }
        catch (Exception ex)
        {
            Notify.Error(T("panelContent.exportFailed", ex.Message));
        }
    }

    public static bool TryImportFromClipboardAsNewPage(out string error)
    {
        error = string.Empty;
        try
        {
            var json = Winforms.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json))
            {
                error = T("panelContent.error.noClipboard");
                return false;
            }

            if (!TryImport(json, out var imported, out error))
                return false;

            C.EnsureDefaults();
            C.Pages.Add(imported);
            EzConfig.Save();
            Notify.Success(T("panelContent.importNewSuccess"));
            return true;
        }
        catch (Exception ex)
        {
            error = T("panelContent.importFailed", ex.Message);
            return false;
        }
    }

    public static bool TryImportFromClipboardToPage(int pageIndex, out string error)
    {
        error = string.Empty;
        try
        {
            var json = Winforms.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json))
            {
                error = T("panelContent.error.noClipboard");
                return false;
            }

            if (!TryImport(json, out var imported, out error))
                return false;

            C.EnsureDefaults();
            if (pageIndex < 0 || pageIndex >= C.Pages.Count)
            {
                error = T("panelContent.error.noPage");
                return false;
            }

            var target = C.Pages[pageIndex];
            var pageName = target.Name;
            var cloned = ClonePage(imported);
            cloned.Name = pageName;
            target.Slots = cloned.Slots;
            EzConfig.Save();
            Notify.Success(T("panelContent.importPageSuccess", pageName));
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

    public static bool TryImport(string json, out PanelPage page, out string error)
    {
        page = Configuration.CreateEmptyPage();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = T("panelContent.error.emptyJson");
            return false;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<PanelContentSnapshot>(json, JsonOptions);
            if (snapshot == null)
            {
                error = T("panelContent.error.loadFailed");
                return false;
            }

            if (snapshot.Version == 0)
                snapshot.Version = PanelContentSnapshot.CurrentVersion;

            if (snapshot.Version != PanelContentSnapshot.CurrentVersion)
            {
                error = T("panelContent.error.unsupportedVersion", snapshot.Version);
                return false;
            }

            page = snapshot.ToPage();
            return true;
        }
        catch (JsonException ex)
        {
            error = T("panelContent.error.invalidJson", ex.Message);
            return false;
        }
    }

    /// <summary>Serializable snapshot of a page's slots.</summary>
    internal sealed class PanelContentSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string PageName { get; set; } = string.Empty;

        public List<PanelSlot> Slots { get; set; } = [];

        public static PanelContentSnapshot From(PanelPage page) =>
            new()
            {
                PageName = page.Name,
                Slots = page.Slots
                    .Take(Configuration.SlotsPerPage)
                    .Select(slot => CloneSlot(slot))
                    .ToList(),
            };

        public PanelPage ToPage()
        {
            var page = Configuration.CreateEmptyPage(PageName);
            page.Slots.Clear();

            for (var i = 0; i < Configuration.SlotsPerPage; i++)
            {
                page.Slots.Add(
                    i < Slots.Count
                        ? CloneSlot(Slots[i])
                        : new PanelSlot());
            }

            return page;
        }

        internal static PanelSlot CloneSlot(PanelSlot slot) =>
            JsonSerializer.Deserialize<PanelSlot>(JsonSerializer.Serialize(slot, JsonOptions), JsonOptions)
            ?? new PanelSlot();
    }
}

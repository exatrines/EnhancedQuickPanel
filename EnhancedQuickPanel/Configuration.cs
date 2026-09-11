using EnhancedQuickPanel.Models;

namespace EnhancedQuickPanel;

/// <summary>Persisted plugin settings: pages, slots, styles, display mode, and context-menu options.</summary>
public sealed class Configuration
{
    public const string FileName = "DefaultConfig.json";
    public const int CurrentConfigVersion = 1;
    public const int GridSize = PanelLayout.BlockSize;
    public const int MinPageCount = 4;
    public const int NativePageCount = 4;
    public const int NativeSlotsPerPage = GridSize * GridSize;

    public void Save() => ConfigurationStore.SaveActive(this);

    public int ConfigVersion { get; set; } = CurrentConfigVersion;

    public bool Enabled { get; set; } = true;

    public bool IsCollapsed { get; set; }

    public PanelDisplayMode DisplayMode { get; set; } = PanelDisplayMode.PluginOnly;

    public bool ShowEditButton { get; set; }

    public bool ShowCollapseButton { get; set; } = true;

    public bool PluginMiddleClickTogglesEnabled { get; set; } = true;

    public bool PluginRightClickOpensSlotMenu { get; set; } = true;

    public int LayoutBlockColumns { get; set; } = 1;

    public int LayoutBlockRows { get; set; } = 1;

    public int GridColumns => PanelLayout.Columns(LayoutBlockColumns);

    public int GridRows => PanelLayout.Rows(LayoutBlockRows);

    public int SlotsPerPage => GridColumns * GridRows;

    public float ComputeGridWidth() =>
        PanelLayout.ComputeSpan(GridColumns, SlotSize, SlotPadding);

    public float ComputeGridHeight() =>
        PanelLayout.ComputeSpan(GridRows, SlotSize, SlotPadding);

    public bool? ShowPageSelectorPopup { get; set; }

    public bool? ShowEmptySlotBorder { get; set; }

    public float OverlayPosX { get; set; } = 200f;

    public float OverlayPosY { get; set; } = 200f;

    public float SlotSize { get; set; } = 40f;

    public float SlotPadding { get; set; } = 2f;

    public float WindowPadding { get; set; } = 15f;

    public float WindowBgRed { get; set; }

    public float WindowBgGreen { get; set; }

    public float WindowBgBlue { get; set; }

    public float WindowBgAlpha { get; set; } = 0.1f;

    public float SlotBgRed { get; set; } = 0.24f;

    public float SlotBgGreen { get; set; } = 0.24f;

    public float SlotBgBlue { get; set; } = 0.25f;

    public float SlotBgAlpha { get; set; } = 0.1f;

    public float SlotDropTargetRed { get; set; } = 0.45f;

    public float SlotDropTargetGreen { get; set; } = 0.62f;

    public float SlotDropTargetBlue { get; set; } = 0.28f;

    public float SlotDropTargetAlpha { get; set; } = 0.55f;

    public float SelectedSlotBorderRed { get; set; } = 0.26f;

    public float SelectedSlotBorderGreen { get; set; } = 0.59f;

    public float SelectedSlotBorderBlue { get; set; } = 0.98f;

    public float SelectedSlotBorderAlpha { get; set; } = 1f;

    public float TooltipBgRed { get; set; } = 0.08f;

    public float TooltipBgGreen { get; set; } = 0.08f;

    public float TooltipBgBlue { get; set; } = 0.08f;

    public float TooltipBgAlpha { get; set; } = 0.9f;

    public float TooltipTextRed { get; set; } = 1f;

    public float TooltipTextGreen { get; set; } = 1f;

    public float TooltipTextBlue { get; set; } = 1f;

    public float TooltipTextAlpha { get; set; } = 1f;

    public float TooltipPadding { get; set; } = 8f;

    public ContextMenuStyleConfig ContextMenu { get; set; } = ContextMenuStyleConfig.CreateDefault();

    public ContextMenuItemsConfig ContextMenuItems { get; set; } = ContextMenuItemsConfig.CreateDefault();

    public OverlayLabelStyleConfig CooldownLabelStyle { get; set; } = OverlayLabelStyleConfig.CreateDefaultText();

    public OverlayLabelStyleConfig ChargeLabelStyle { get; set; } = OverlayLabelStyleConfig.CreateDefaultText();

    public OverlayLabelStyleConfig QuantityLabelStyle { get; set; } = OverlayLabelStyleConfig.CreateDefaultText();

    public OverlayLabelStyleConfig MacroGearLabelStyle { get; set; } = OverlayLabelStyleConfig.CreateDefaultMacroGear();

    public PageBarButtonStyleConfig PageBarButtons { get; set; } = PageBarButtonStyleConfig.CreateDefault();

    public PanelUiStyleConfig PanelUi { get; set; } = PanelUiStyleConfig.CreateDefault();

    public float EditModeWindowBgRed { get; set; }

    public float EditModeWindowBgGreen { get; set; }

    public float EditModeWindowBgBlue { get; set; }

    public float EditModeWindowBgAlpha { get; set; } = 0.1f;

    public float WindowBorderRed { get; set; } = 1f;

    public float WindowBorderGreen { get; set; } = 1f;

    public float WindowBorderBlue { get; set; } = 1f;

    public float WindowBorderAlpha { get; set; } = 0.35f;

    public float WindowBorderThickness { get; set; } = 1f;

    public float EditModeWindowBorderRed { get; set; } = 1f;

    public float EditModeWindowBorderGreen { get; set; } = 1f;

    public float EditModeWindowBorderBlue { get; set; } = 1f;

    public float EditModeWindowBorderAlpha { get; set; } = 0.55f;

    public float EditModeWindowBorderThickness { get; set; } = 1f;

    public Vector4 WindowBgColor => new(WindowBgRed, WindowBgGreen, WindowBgBlue, WindowBgAlpha);

    public Vector4 SlotBgColor => new(SlotBgRed, SlotBgGreen, SlotBgBlue, SlotBgAlpha);

    public Vector4 SlotDropTargetColor =>
        new(SlotDropTargetRed, SlotDropTargetGreen, SlotDropTargetBlue, SlotDropTargetAlpha);

    public Vector4 SelectedSlotBorderColor =>
        new(SelectedSlotBorderRed, SelectedSlotBorderGreen, SelectedSlotBorderBlue, SelectedSlotBorderAlpha);

    public Vector4 TooltipBgColor => new(TooltipBgRed, TooltipBgGreen, TooltipBgBlue, TooltipBgAlpha);

    public Vector4 TooltipTextColor => new(TooltipTextRed, TooltipTextGreen, TooltipTextBlue, TooltipTextAlpha);

    public Vector4 EditModeWindowBgColor =>
        new(EditModeWindowBgRed, EditModeWindowBgGreen, EditModeWindowBgBlue, EditModeWindowBgAlpha);

    public Vector4 WindowBorderColor =>
        new(WindowBorderRed, WindowBorderGreen, WindowBorderBlue, WindowBorderAlpha);

    public Vector4 EditModeWindowBorderColor =>
        new(EditModeWindowBorderRed, EditModeWindowBorderGreen, EditModeWindowBorderBlue, EditModeWindowBorderAlpha);

    public void SetWindowBgColor(Vector4 color)
    {
        WindowBgRed = color.X;
        WindowBgGreen = color.Y;
        WindowBgBlue = color.Z;
        WindowBgAlpha = color.W;
    }

    public void SetSlotBgColor(Vector4 color)
    {
        SlotBgRed = color.X;
        SlotBgGreen = color.Y;
        SlotBgBlue = color.Z;
        SlotBgAlpha = color.W;
    }

    public void SetTooltipBgColor(Vector4 color)
    {
        TooltipBgRed = color.X;
        TooltipBgGreen = color.Y;
        TooltipBgBlue = color.Z;
        TooltipBgAlpha = color.W;
    }

    public void SetTooltipTextColor(Vector4 color)
    {
        TooltipTextRed = color.X;
        TooltipTextGreen = color.Y;
        TooltipTextBlue = color.Z;
        TooltipTextAlpha = color.W;
    }

    public void SetEditModeWindowBgColor(Vector4 color)
    {
        EditModeWindowBgRed = color.X;
        EditModeWindowBgGreen = color.Y;
        EditModeWindowBgBlue = color.Z;
        EditModeWindowBgAlpha = color.W;
    }

    public void SetWindowBorderColor(Vector4 color)
    {
        WindowBorderRed = color.X;
        WindowBorderGreen = color.Y;
        WindowBorderBlue = color.Z;
        WindowBorderAlpha = color.W;
    }

    public void SetEditModeWindowBorderColor(Vector4 color)
    {
        EditModeWindowBorderRed = color.X;
        EditModeWindowBorderGreen = color.Y;
        EditModeWindowBorderBlue = color.Z;
        EditModeWindowBorderAlpha = color.W;
    }

    public List<PanelPage> Pages { get; set; } = CreateDefaultPages();

    public static Configuration CreateNew()
    {
        var config = new Configuration { ConfigVersion = CurrentConfigVersion };
        config.FillMissing();
        config.Validate();
        return config;
    }

    public static List<PanelPage> CreateDefaultPages() =>
        CreateDefaultPages(NativeSlotsPerPage);

    public static List<PanelPage> CreateDefaultPages(int slotCount)
    {
        var pages = new List<PanelPage>(MinPageCount);
        for (var i = 0; i < MinPageCount; i++)
            pages.Add(CreateEmptyPage(slotCount, T("page.defaultName", i + 1)));

        return pages;
    }

    public void EnsureDefaults()
    {
        FillMissing();
        Validate();
    }

    public void FillMissing()
    {
        Pages ??= CreateDefaultPages(SlotsPerPage);

        if (Pages.Count == 0)
            Pages.Add(CreateEmptyPage(SlotsPerPage, T("page.defaultName", 1)));

        for (var i = 0; i < Pages.Count; i++)
        {
            FillPageSlots(Pages[i], SlotsPerPage);
            if (string.IsNullOrWhiteSpace(Pages[i].Name))
                Pages[i].Name = T("page.defaultName", i + 1);
        }

        CooldownLabelStyle ??= OverlayLabelStyleConfig.CreateDefaultText();
        ChargeLabelStyle ??= OverlayLabelStyleConfig.CreateDefaultText();
        QuantityLabelStyle ??= OverlayLabelStyleConfig.CreateDefaultText();
        MacroGearLabelStyle ??= OverlayLabelStyleConfig.CreateDefaultMacroGear();
        PageBarButtons ??= PageBarButtonStyleConfig.CreateDefault();
        PanelUi ??= PanelUiStyleConfig.FromPageBarStyle(PageBarButtons);
        ContextMenu ??= ContextMenuStyleConfig.CreateDefault();
        ContextMenuItems ??= ContextMenuItemsConfig.CreateDefault();
    }

    public void Validate()
    {
        if (DisplayMode != PanelDisplayMode.NativeOnly && DisplayMode != PanelDisplayMode.PluginOnly)
            DisplayMode = PanelDisplayMode.PluginOnly;

        LayoutBlockColumns = PanelLayout.ClampBlocks(LayoutBlockColumns);
        LayoutBlockRows = PanelLayout.ClampBlocks(LayoutBlockRows);

        ContextMenu?.EnsureDefaults();

        if (Pages is null)
            return;

        foreach (var page in Pages)
        {
            if (page.Slots is null)
                continue;

            foreach (var slot in page.Slots)
                slot.Sanitize();
        }
    }

    public void AddPage(string? name = null)
    {
        EnsureDefaults();
        var pageName = string.IsNullOrWhiteSpace(name)
            ? GetNextPageName()
            : name.Trim();
        Pages.Add(CreateEmptyPage(SlotsPerPage, pageName));
    }

    public string GetNextPageName()
    {
        EnsureDefaults();

        return T("page.defaultName", Pages.Count + 1);
    }

    public bool CanRemovePage(int pageIndex) =>
        pageIndex >= 0 && pageIndex < Pages.Count && Pages.Count > 1;

    public bool TryRemovePage(int pageIndex)
    {
        if (!CanRemovePage(pageIndex))
            return false;

        Pages.RemoveAt(pageIndex);
        return true;
    }

    public static PanelPage CreateEmptyPage(int slotCount, string? name = null)
    {
        var page = new PanelPage
        {
            Name = name ?? string.Empty,
        };

        FillPageSlots(page, slotCount);
        return page;
    }

    private static void FillPageSlots(PanelPage page, int slotCount)
    {
        page.Slots ??= [];
        while (page.Slots.Count < slotCount)
            page.Slots.Add(new PanelSlot());
    }
}

using System.Text.Json;
using EnhancedQuickPanel.Models;

// Existing saved numeric kinds must retain their meanings after adding plugin slots.
var legacy = JsonSerializer.Deserialize<PanelPage>("""
    {"Name":"Legacy","Slots":[{"Kind":0},{"Kind":1,"CommandId":123},
    {"Kind":2,"MacroIndex":99},{"Kind":3,"TextBody":"/echo legacy"}]}
    """)!;
Require(legacy.Slots.Select(s => s.Kind).SequenceEqual(new[] {
    PanelSlotKind.Empty, PanelSlotKind.Action, PanelSlotKind.Macro, PanelSlotKind.TextCommand }), "Legacy kinds");
Require(legacy.Slots.Skip(1).All(s => s.IsConfigured), "Legacy configuration");
Require(!new PanelSlot { Kind = PanelSlotKind.Plugin }.IsConfigured, "Unselected plugin");

var page = new PanelPage { Name = "Plugins", Slots = [new PanelSlot {
    Kind = PanelSlotKind.Plugin, PluginInternalName = "Example", IconId = uint.MaxValue,
    LeftClickAction = PluginShortcutAction.TextCommand, LeftClickCommand = "/echo left",
    RightClickAction = PluginShortcutAction.None, RightClickCommand = "/echo right",
    MiddleClickAction = PluginShortcutAction.ConfigUi, MiddleClickCommand = "/echo middle",
}] };
var encoded = JsonSerializer.Serialize(page);
var restored = JsonSerializer.Deserialize<PanelPage>(encoded)!;
Require(JsonSerializer.Serialize(restored) == encoded && restored.Slots[0].IsConfigured,
    "Page round-trip preserves all bindings and custom icon IDs");
var defaults = JsonSerializer.Deserialize<PanelSlot>("""{"Kind":4,"PluginInternalName":"Example"}""")!;
Require(defaults.IconId == 0 && defaults.LeftClickAction == PluginShortcutAction.MainUi
    && defaults.RightClickAction == PluginShortcutAction.ConfigUi
    && defaults.MiddleClickAction == PluginShortcutAction.ToggleEnabled, "Missing fields use defaults");
Require(new PanelSlot().MiddleClickAction == PluginShortcutAction.ToggleEnabled,
    "New slots default middle click to enable/disable");
var keptNone = JsonSerializer.Deserialize<PanelSlot>(
    """{"Kind":4,"PluginInternalName":"Example","MiddleClickAction":0}""")!;
Require(keptNone.MiddleClickAction == PluginShortcutAction.None, "Explicit do-nothing is preserved");
var keepToggle = new PanelSlot { MiddleClickAction = PluginShortcutAction.ToggleEnabled };
keepToggle.SanitizePluginShortcut();
Require(keepToggle.MiddleClickAction == PluginShortcutAction.ToggleEnabled,
    "Enable/disable is not stripped by sanitization");

var leftoverCollapse = JsonSerializer.Deserialize<PanelSlot>(
    """{"Kind":4,"PluginInternalName":"Example","MiddleClickAction":4}""")!;
Require((int)leftoverCollapse.MiddleClickAction == 4 && !Enum.IsDefined(leftoverCollapse.MiddleClickAction),
    "Retired collapse value 4 remains undefined");
leftoverCollapse.SanitizePluginShortcut();
Require(leftoverCollapse.MiddleClickAction == PluginShortcutAction.None,
    "Undefined click action maps to do-nothing");

Require(new PanelSlot { Kind = PanelSlotKind.Dalamud }.DalamudShortcut == DalamudShortcutKind.Plugins
    && new PanelSlot { Kind = PanelSlotKind.Dalamud }.IsConfigured, "Dalamud defaults to plugin installer");
var dalamud = JsonSerializer.Deserialize<PanelSlot>("""{"Kind":5}""")!;
Require(dalamud.Kind == PanelSlotKind.Dalamud
    && dalamud.DalamudShortcut == DalamudShortcutKind.Plugins
    && dalamud.IsConfigured, "Missing DalamudShortcut uses plugin installer");
var migrated = JsonSerializer.Deserialize<PanelSlot>(
    """{"Kind":5,"TextBody":"/xlplugins"}""")!;
migrated.SanitizeDalamudShortcut();
Require(migrated.DalamudShortcut == DalamudShortcutKind.Plugins
    && migrated.TextBody == string.Empty, "Legacy Dalamud TextBody migrates off the command path");

var numbered = new List<PanelSlot>();
for (var i = 0; i < 25; i++)
    numbered.Add(new PanelSlot { Kind = PanelSlotKind.TextCommand, TextBody = i.ToString() });

var keepWide = PanelLayout.RemapKeepPositions(numbered, 5, 5, 10, 5);
Require(keepWide.Count == 50, "Keep-positions 2×1 size");
Require(keepWide[0].TextBody == "0" && keepWide[4].TextBody == "4" && keepWide[5].TextBody == ""
    && keepWide[10].TextBody == "5" && keepWide[14].TextBody == "9", "Keep-positions 5×5 to 10×5");

var keepTall = PanelLayout.RemapKeepPositions(numbered, 5, 5, 5, 10);
Require(keepTall.Count == 50 && keepTall[5].TextBody == "5" && keepTall[24].TextBody == "24"
    && keepTall[25].TextBody == "", "Keep-positions 5×5 to 5×10");

var wideWithRightSlot = PanelLayout.RemapKeepPositions(numbered, 5, 5, 10, 5);
wideWithRightSlot[5] = new PanelSlot { Kind = PanelSlotKind.TextCommand, TextBody = "right" };
Require(PanelLayout.HasConfiguredOutside(wideWithRightSlot, 10, 5, 5, 5),
    "Occupied right block blocks shrink");
wideWithRightSlot[5] = new PanelSlot();
Require(!PanelLayout.HasConfiguredOutside(wideWithRightSlot, 10, 5, 5, 5),
    "Empty right block can shrink");

Require(!PanelLayout.CanFit(10, 5, 5, 10), "2×1 does not fit 1×2");
Require(PanelLayout.CanFit(5, 5, 10, 10), "1×1 fits 2×2");

var dest = PanelLayout.CreateSlots(100);
dest[99] = new PanelSlot { Kind = PanelSlotKind.TextCommand, TextBody = "keep" };
PanelLayout.PasteTopLeft(dest, 10, 10, numbered, 5, 5);
Require(dest[0].TextBody == "0" && dest[4].TextBody == "4" && dest[10].TextBody == "5"
    && dest[99].TextBody == "keep", "Paste top-left keeps cells outside the source");

StorePipeline.Run();

Console.WriteLine("Configuration compatibility checks passed.");

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
}

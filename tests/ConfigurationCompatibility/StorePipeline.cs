using System.Text;
using EnhancedQuickPanel;
using EnhancedQuickPanel.Models;
using Newtonsoft.Json.Linq;

internal static class StorePipeline
{
    private static readonly TimeProvider Stamp = new FixedTimeProvider(
        new DateTimeOffset(2026, 9, 11, 15, 30, 0, TimeSpan.FromHours(9)));

    public static void Run()
    {
        NewFileIsV1AndNotWritten();
        MissingVersionMigratesPublishedValues();
        ConfigVersionZeroMigrates();
        ExplicitFalseAndZeroAreKept();
        PresentLayoutIsNotReset();
        ExtraSlotsAreNotRemoved();
        PlaceholderZerosAreKept();
        MissingPanelUiColorsUseInitializers();
        CurrentVersionDoesNotRemigrate();
        FutureAndInvalidLeaveOriginal();
        BackupAndWriteFailuresLeaveOriginal();
        SavedConfigCanReload();
    }

    private static void NewFileIsV1AndNotWritten()
    {
        var dir = TempDir();
        try
        {
            var config = Store(dir).Load();
            Require(config.ConfigVersion == Configuration.CurrentConfigVersion, "New config is v1");
            Require(config.LayoutBlockColumns == 1 && config.LayoutBlockRows == 1, "New config layout is 5×5");
            Require(!File.Exists(Path.Combine(dir, Configuration.FileName)), "New config is not written until Save");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void MissingVersionMigratesPublishedValues()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, PublishedV0Json);
            var original = File.ReadAllBytes(Path.Combine(dir, Configuration.FileName));
            var config = Store(dir).Load();
            Require(config.ConfigVersion == 1, "Migrated config is v1");
            Require(config.Enabled == false, "Published Enabled false is kept");
            Require(config.DisplayMode == PanelDisplayMode.NativeOnly, "Published DisplayMode is kept");
            Require(config.ShowEditButton == false, "Published ShowEditButton false is kept");
            Require(config.OverlayPosX == 0f && config.OverlayPosY == 200f, "Published overlay position is kept");
            Require(config.WindowBgAlpha == 0f, "Published WindowBgAlpha 0 is kept");
            Require(config.LayoutBlockColumns == 1 && config.LayoutBlockRows == 1, "Missing layout becomes 5×5");
            Require(config.Pages.Count == 1 && config.Pages[0].Name == "Combat", "Published page is kept");
            Require(config.Pages[0].Slots[0].TextBody == "/echo keep", "Published slot is kept");
            Require(config.Pages[0].Slots[0].Kind == PanelSlotKind.TextCommand, "Published slot kind is kept");
            Require(config.Pages[0].Slots[0].IconId == 42, "Published custom icon id is kept");
            Require(config.Pages[0].Slots.Count >= 25, "Missing slots are padded to 5×5");
            Require(config.ShowCollapseButton, "Missing ShowCollapseButton uses current default");
            Require(File.Exists(BackupPath(dir)), "Migration writes a v0 backup");
            Require(
                original.SequenceEqual(File.ReadAllBytes(BackupPath(dir))),
                "Backup is the original file bytes");

            var saved = JObject.Parse(ReadConfig(dir));
            Require(saved.Value<int>("ConfigVersion") == 1, "Migrated file stores ConfigVersion 1");
            Require(saved.Value<bool>("Enabled") == false, "Migrated file keeps Enabled false");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void ConfigVersionZeroMigrates()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "ConfigVersion": 0,
                  "Enabled": false,
                  "Pages": [{ "Name": "Zero", "Slots": [{ "Kind": 3, "TextBody": "/echo zero" }] }]
                }
                """);
            var config = Store(dir).Load();
            Require(config.ConfigVersion == 1 && !config.Enabled, "Integer 0 migrates to v1");
            Require(config.Pages[0].Slots[0].TextBody == "/echo zero", "ConfigVersion 0 keeps slots");
            Require(File.Exists(BackupPath(dir)), "ConfigVersion 0 writes a backup");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void ExplicitFalseAndZeroAreKept()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "Enabled": false,
                  "ShowEditButton": false,
                  "ShowCollapseButton": false,
                  "PluginMiddleClickTogglesEnabled": false,
                  "PluginRightClickOpensSlotMenu": false,
                  "PluginPickerUsesPopup": false,
                  "OverlayPosX": 0,
                  "WindowBgAlpha": 0,
                  "Pages": [{ "Name": "A", "Slots": [{ "Kind": 0 }] }]
                }
                """);
            var config = Store(dir).Load();
            Require(!config.Enabled && !config.ShowEditButton && !config.ShowCollapseButton, "Explicit false flags are kept");
            Require(
                !config.PluginMiddleClickTogglesEnabled && !config.PluginRightClickOpensSlotMenu,
                "Explicit plugin click flags are kept");
            Require(config.OverlayPosX == 0f && config.WindowBgAlpha == 0f, "Explicit zeros are kept");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void PresentLayoutIsNotReset()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "LayoutBlockColumns": 2,
                  "LayoutBlockRows": 1,
                  "Pages": [{ "Name": "Wide", "Slots": [] }]
                }
                """);
            var config = Store(dir).Load();
            Require(config.LayoutBlockColumns == 2 && config.LayoutBlockRows == 1, "Present layout is not reset");
            Require(config.Pages[0].Slots.Count == 50, "Slots follow the present 10×5 layout");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void ExtraSlotsAreNotRemoved()
    {
        var dir = TempDir();
        try
        {
            var slots = new StringBuilder();
            for (var i = 0; i < 30; i++)
            {
                if (i > 0)
                    slots.Append(',');
                slots.Append($@"{{""Kind"":3,""TextBody"":""{i}""}}");
            }

            WriteConfig(dir, $$"""
                { "Pages": [{ "Name": "Extra", "Slots": [{{slots}}] }] }
                """);
            var config = Store(dir).Load();
            Require(config.Pages[0].Slots.Count == 30, "Extra slots are not removed");
            Require(config.Pages[0].Slots[29].TextBody == "29", "Trailing extra slot is kept");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void PlaceholderZerosAreKept()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "ConfigVersion": 1,
                  "PanelUi": {
                    "PlaceholderTextRed": 0,
                    "PlaceholderTextGreen": 0,
                    "PlaceholderTextBlue": 0,
                    "PlaceholderTextAlpha": 0,
                    "DropdownBgRed": 0,
                    "DropdownBgGreen": 0,
                    "DropdownBgBlue": 0,
                    "DropdownBgAlpha": 0
                  },
                  "Pages": [{ "Name": "A", "Slots": [{}] }]
                }
                """);
            var config = Store(dir).Load();
            Require(
                config.PanelUi.PlaceholderTextRed == 0f
                && config.PanelUi.PlaceholderTextGreen == 0f
                && config.PanelUi.PlaceholderTextBlue == 0f
                && config.PanelUi.PlaceholderTextAlpha == 0f,
                "Explicit placeholder zeros are kept");
            Require(
                config.PanelUi.DropdownBgRed == 0f
                && config.PanelUi.DropdownBgGreen == 0f
                && config.PanelUi.DropdownBgBlue == 0f
                && config.PanelUi.DropdownBgAlpha == 0f,
                "Explicit dropdown zeros are kept");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void MissingPanelUiColorsUseInitializers()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "ConfigVersion": 1,
                  "PanelUi": { "TextRed": 0.5 },
                  "Pages": [{ "Name": "A", "Slots": [{}] }]
                }
                """);
            var config = Store(dir).Load();
            Require(config.PanelUi.TextRed == 0.5f, "Present PanelUi text is kept");
            Require(
                config.PanelUi.PlaceholderTextRed == 0.6f
                && config.PanelUi.PlaceholderTextAlpha == 1f,
                "Missing placeholder uses initializer");
            Require(
                config.PanelUi.DropdownBgRed == 0.2f
                && config.PanelUi.DropdownBgAlpha == 1f,
                "Missing dropdown uses initializer");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void CurrentVersionDoesNotRemigrate()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, """
                {
                  "ConfigVersion": 1,
                  "Enabled": false,
                  "Pages": [{ "Name": "Current", "Slots": [{ "Kind": 3, "TextBody": "/echo v1" }] }]
                }
                """);
            var original = ReadConfig(dir);
            var config = Store(dir).Load();
            Require(config.ConfigVersion == 1 && !config.Enabled, "v1 loads without rewriting flags");
            Require(config.Pages[0].Slots[0].TextBody == "/echo v1", "v1 slot is kept");
            Require(ReadConfig(dir) == original, "v1 load does not rewrite the file");
            Require(Directory.GetFiles(dir, "DefaultConfig.v*-backup-*.json").Length == 0, "v1 load creates no backup");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void FutureAndInvalidLeaveOriginal()
    {
        AssertUnchanged("{\"ConfigVersion\":2}", "Unsupported v2");
        AssertUnchanged("{\"ConfigVersion\":0.5}", "Fractional ConfigVersion");
        AssertUnchanged("{\"ConfigVersion\":\"1\"}", "String ConfigVersion");
        AssertUnchanged("{\"ConfigVersion\":null}", "Null ConfigVersion");
        AssertUnchanged("{\"ConfigVersion\":-1}", "Negative ConfigVersion");
        AssertUnchanged("null", "JSON null");
        AssertUnchanged("{", "Truncated JSON");
        AssertUnchanged("[]", "JSON array");
        AssertUnchanged("""{"ConfigVersion":1,"Pages":[null]}""", "Null page entry");
    }

    private static void BackupAndWriteFailuresLeaveOriginal()
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, PublishedV0Json);
            var original = File.ReadAllBytes(Path.Combine(dir, Configuration.FileName));
            Directory.CreateDirectory(BackupPath(dir));
            RequireThrows(() => Store(dir).Load(), "Backup path that is a directory aborts migration", Path.Combine(dir, Configuration.FileName));
            Require(
                original.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, Configuration.FileName))),
                "Original remains when backup fails");
        }
        finally
        {
            DeleteDir(dir);
        }

        dir = TempDir();
        try
        {
            WriteConfig(dir, PublishedV0Json);
            var original = File.ReadAllBytes(Path.Combine(dir, Configuration.FileName));
            Directory.CreateDirectory(Path.Combine(dir, Configuration.FileName + ".new"));
            RequireThrows(() => Store(dir).Load(), "Blocked temp file aborts migration write", Path.Combine(dir, Configuration.FileName));
            Require(
                original.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, Configuration.FileName))),
                "Original remains when migrated write fails");
            Require(File.Exists(BackupPath(dir)), "Backup can exist when write fails after copy");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void SavedConfigCanReload()
    {
        var dir = TempDir();
        try
        {
            var store = Store(dir);
            var created = store.Load();
            created.Enabled = false;
            created.OverlayPosX = 0f;
            created.Pages[0].Slots[0].Kind = PanelSlotKind.TextCommand;
            created.Pages[0].Slots[0].TextBody = "/echo saved";
            store.Save(created);

            var loaded = store.Load();
            Require(loaded.ConfigVersion == 1, "Saved config reloads as v1");
            Require(!loaded.Enabled && loaded.OverlayPosX == 0f, "Saved false and zero reload");
            Require(loaded.Pages[0].Slots[0].TextBody == "/echo saved", "Saved slot reloads");
            Require(Directory.GetFiles(dir, "DefaultConfig.v*-backup-*.json").Length == 0, "Reload of saved v1 does not back up");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void AssertUnchanged(string json, string name)
    {
        var dir = TempDir();
        try
        {
            WriteConfig(dir, json);
            var path = Path.Combine(dir, Configuration.FileName);
            var original = File.ReadAllBytes(path);
            RequireThrows(() => Store(dir).Load(), name + " aborts load", path);
            Require(
                original.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, Configuration.FileName))),
                name + " leaves the original file");
            Require(
                Directory.GetFiles(dir, "DefaultConfig.v*-backup-*.json").Length == 0,
                name + " creates no backup");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static ConfigurationStore Store(string dir) => new(dir, Stamp);

    private static string BackupPath(string dir) =>
        Path.Combine(dir, "DefaultConfig.v0-backup-20260911-153000.json");

    private static void WriteConfig(string dir, string json) =>
        File.WriteAllText(Path.Combine(dir, Configuration.FileName), json, Encoding.UTF8);

    private static string ReadConfig(string dir) =>
        File.ReadAllText(Path.Combine(dir, Configuration.FileName), Encoding.UTF8);

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqp-config-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
    }

    private static void RequireThrows(Action action, string name, string? path = null)
    {
        try
        {
            action();
        }
        catch (ConfigurationLoadException ex)
        {
            if (path != null)
            {
                Require(ex.ConfigPath == path, name + " sets ConfigPath");
                Require(
                    ex.Message.Contains(path, StringComparison.Ordinal),
                    name + " message includes the config path");
            }

            return;
        }

        throw new InvalidOperationException(name + " did not throw ConfigurationLoadException");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
    }

    private const string PublishedV0Json = """
        {
          "Enabled": false,
          "DisplayMode": 0,
          "ShowEditButton": false,
          "OverlayPosX": 0.0,
          "OverlayPosY": 200.0,
          "SlotSize": 40.0,
          "WindowBgAlpha": 0.0,
          "Pages": [
            {
              "Name": "Combat",
              "Slots": [
                { "Kind": 3, "TextBody": "/echo keep", "IconId": 42 }
              ]
            }
          ]
        }
        """;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _localNow;

        public FixedTimeProvider(DateTimeOffset localNow)
        {
            _localNow = localNow;
            LocalTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                "eqp-config-test",
                localNow.Offset,
                "eqp-config-test",
                "eqp-config-test");
        }

        public override TimeZoneInfo LocalTimeZone { get; }

        public override DateTimeOffset GetUtcNow() => _localNow.ToUniversalTime();
    }
}

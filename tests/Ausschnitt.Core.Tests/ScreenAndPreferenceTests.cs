using Ausschnitt.Core;

namespace Ausschnitt.Core.Tests;

public class ScreenAndPreferenceTests
{
    [Fact]
    public void Drag_normalizes_and_shift_locks_aspect()
    {
        var dragged = ScreenRect.FromPoints(300, 200, 100, 80);
        Assert.Equal(new ScreenRect(100, 80, 200, 120), dragged);

        var locked = ScreenRect.ConstrainAspect(0, 0, 160, 200, 1.6);
        Assert.Equal(160, locked.Width);
        Assert.Equal(100, locked.Height);
    }

    [Fact]
    public void Union_includes_a_monitor_left_of_the_primary()
    {
        var box = ScreenRect.Union(
        [
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(-1280, 100, 1280, 800),
        ]);
        Assert.Equal(-1280, box.X);
        Assert.Equal(0, box.Y);
        Assert.Equal(1920 + 1280, box.Width);
    }

    [Fact]
    public void Preference_text_updates_every_mapping_block()
    {
        const string original = """
            SystemOriginX 0
            SystemOriginY 0
            SystemExtentX 1920
            SystemExtentY 1080
            SomethingElse 5
            SystemOriginX 0
            SystemOriginY 0
            SystemExtentX 1920
            SystemExtentY 1080
            """;

        var outcome = WacomPreferencePatcher.Patch(original, new ScreenRect(-100, 40, 800, 500));

        Assert.Equal(PreferencePatchStatus.Updated, outcome.Status);
        Assert.Equal(8, outcome.Replacements);
        Assert.Contains("SystemOriginX -100", outcome.Text);
        Assert.Contains("SystemOriginY 40", outcome.Text);
        Assert.Contains("SystemExtentX 800", outcome.Text);
        Assert.Contains("SystemExtentY 500", outcome.Text);
        Assert.DoesNotContain("SystemExtentX 1920", outcome.Text);
        Assert.Contains("SomethingElse 5", outcome.Text);
    }

    [Fact]
    public void Preference_xml_and_missing_keys()
    {
        const string xml = "<SystemOriginX>0</SystemOriginX><SystemOriginY>0</SystemOriginY><SystemExtentX>10</SystemExtentX><SystemExtentY>10</SystemExtentY>";
        var outcome = WacomPreferencePatcher.Patch(xml, new ScreenRect(3, 4, 5, 6));
        Assert.Equal(PreferencePatchStatus.Updated, outcome.Status);
        Assert.Contains("<SystemOriginX>3</SystemOriginX>", outcome.Text);
        Assert.Contains("<SystemExtentY>6</SystemExtentY>", outcome.Text);

        var missing = WacomPreferencePatcher.Patch("no mapping here", new ScreenRect(1, 2, 3, 4));
        Assert.Equal(PreferencePatchStatus.KeysMissing, missing.Status);
    }

    [Fact]
    public void Binary_preference_is_left_alone()
    {
        var outcome = WacomPreferencePatcher.Patch("SystemOriginX 0\0SystemOriginY 0", new ScreenRect(1, 1, 1, 1));
        Assert.Equal(PreferencePatchStatus.Binary, outcome.Status);
    }

    [Fact]
    public void Preference_file_roundtrip_keeps_a_backup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ausschnitt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "Wacom_Tablet.dat");
        File.WriteAllText(path, "SystemOriginX 1\nSystemOriginY 2\nSystemExtentX 3\nSystemExtentY 4\n");

        Assert.True(WacomPreferencePatcher.LooksPatchable(path));
        var outcome = WacomPreferencePatcher.PatchFile(path, new ScreenRect(9, 8, 7, 6));
        Assert.Equal(PreferencePatchStatus.Updated, outcome.Status);
        Assert.Contains("SystemOriginX 9", File.ReadAllText(path));
        Assert.Contains("SystemExtentY 4", File.ReadAllText(path + ".ausschnitt-original"));
    }
}

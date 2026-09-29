using System.Text.Json;
using Ausschnitt.Core;

namespace Ausschnitt.Session;

public enum EnginePreference
{
    Auto = 0,
    Driver = 1,
    Direct = 2,
}

public sealed class AppSettings
{
    public int HotkeyModifiers { get; set; } = 0x0001 | 0x0002;
    public int HotkeyVirtualKey { get; set; } = 0x41;
    public string HotkeyLabel { get; set; } = "Strg + Alt + A";
    public bool KeepProportions { get; set; }
    public bool RunAtStartup { get; set; }
    public bool HasArea { get; set; }
    public int AreaX { get; set; }
    public int AreaY { get; set; }
    public int AreaWidth { get; set; }
    public int AreaHeight { get; set; }
    public string Orientation { get; set; } = nameof(PenOrientation.Normal);
    public EnginePreference Engine { get; set; } = EnginePreference.Auto;

    public ScreenRect? Area => HasArea && AreaWidth >= 8 && AreaHeight >= 8
        ? new ScreenRect(AreaX, AreaY, AreaWidth, AreaHeight)
        : null;

    public void SetArea(ScreenRect? area)
    {
        if (area is not { IsUsable: true } rect)
        {
            HasArea = false;
            return;
        }

        HasArea = true;
        AreaX = rect.X;
        AreaY = rect.Y;
        AreaWidth = rect.Width;
        AreaHeight = rect.Height;
    }
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ausschnitt",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}

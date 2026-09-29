using System.Text;
using System.Text.RegularExpressions;

namespace Ausschnitt.Core;

public enum PreferencePatchStatus
{
    Updated,
    KeysMissing,
    Binary,
}

public readonly record struct PreferencePatchOutcome(PreferencePatchStatus Status, int Replacements, string Text);

/// <summary>
/// Rewrites the screen-mapping block inside a text Wacom_Tablet.dat.
/// Older and current consumer drivers store one line per value:
/// SystemOriginX, SystemOriginY, SystemExtentX, SystemExtentY.
/// Extent is the width and height in pixels, matching Wintab's lcSysExt.
/// </summary>
public static partial class WacomPreferencePatcher
{
    public static PreferencePatchOutcome Patch(string text, ScreenRect area)
    {
        if (text.Contains('\0'))
            return new PreferencePatchOutcome(PreferencePatchStatus.Binary, 0, text);

        int originsX = Count(text, "SystemOriginX");
        int originsY = Count(text, "SystemOriginY");
        int extentsX = Count(text, "SystemExtentX");
        int extentsY = Count(text, "SystemExtentY");
        if (originsX == 0 || originsY == 0 || extentsX == 0 || extentsY == 0)
            return new PreferencePatchOutcome(PreferencePatchStatus.KeysMissing, 0, text);

        int replacements = 0;
        string updated = LinePattern().Replace(text, match => Replace(match, area, ref replacements));
        updated = XmlPattern().Replace(updated, match => Replace(match, area, ref replacements));
        if (replacements == 0)
            return new PreferencePatchOutcome(PreferencePatchStatus.KeysMissing, 0, text);

        return new PreferencePatchOutcome(PreferencePatchStatus.Updated, replacements, updated);
    }

    public static PreferencePatchOutcome PatchFile(string path, ScreenRect area)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (LooksBinary(bytes))
            return new PreferencePatchOutcome(PreferencePatchStatus.Binary, 0, "");

        Encoding encoding = DetectEncoding(bytes, out int preamble);
        string text = encoding.GetString(bytes, preamble, bytes.Length - preamble);
        var outcome = Patch(text, area);
        if (outcome.Status != PreferencePatchStatus.Updated)
            return outcome;

        string backup = path + ".ausschnitt-original";
        if (!File.Exists(backup))
            File.Copy(path, backup);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        if (preamble > 0)
            stream.Write(bytes, 0, preamble);
        byte[] body = encoding.GetBytes(outcome.Text);
        stream.Write(body);
        return outcome;
    }

    public static bool LooksPatchable(string path)
    {
        if (!File.Exists(path))
            return false;
        byte[] bytes = File.ReadAllBytes(path);
        if (LooksBinary(bytes))
            return false;
        Encoding encoding = DetectEncoding(bytes, out int preamble);
        string text = encoding.GetString(bytes, preamble, bytes.Length - preamble);
        return Patch(text, new ScreenRect(0, 0, 1, 1)).Status == PreferencePatchStatus.Updated;
    }

    private static string Replace(Match match, ScreenRect area, ref int replacements)
    {
        string key = match.Groups[1].Value;
        string value = key switch
        {
            "SystemOriginX" => area.X.ToString(),
            "SystemOriginY" => area.Y.ToString(),
            "SystemExtentX" => area.Width.ToString(),
            "SystemExtentY" => area.Height.ToString(),
            _ => match.Groups[3].Value,
        };
        replacements++;
        bool xml = match.Value.Contains('<');
        if (xml)
            return $"<{key}>{match.Groups[2].Value}{value}{match.Groups[4].Value}</{key}>";
        return $"{key}{match.Groups[2].Value}{value}{match.Groups[4].Value}";
    }

    private static int Count(string text, string key)
    {
        return LineKeyPattern(key).Matches(text).Count + XmlKeyPattern(key).Matches(text).Count;
    }

    private static bool LooksBinary(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return false;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return false;

        int sample = Math.Min(bytes.Length, 4096);
        int odd = 0;
        for (int i = 0; i < sample; i++)
        {
            byte b = bytes[i];
            if (b == 0)
                odd++;
            else if (b < 9 || (b > 13 && b < 32))
                odd++;
        }

        return sample > 0 && odd > sample / 20;
    }

    private static Encoding DetectEncoding(byte[] bytes, out int preamble)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            preamble = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            preamble = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        preamble = 0;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private static Regex LineKeyPattern(string key) => new(
        $"^(?:{key})[ \\t]+-?\\d+[ \\t]*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static Regex XmlKeyPattern(string key) => new(
        $"<{key}>\\s*-?\\d+\\s*</{key}>",
        RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^(SystemOriginX|SystemOriginY|SystemExtentX|SystemExtentY)([ \t]+)(-?\d+)([ \t]*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LinePattern();

    [GeneratedRegex(@"<(SystemOriginX|SystemOriginY|SystemExtentX|SystemExtentY)>(\s*)(-?\d+)(\s*)</\1>", RegexOptions.CultureInvariant)]
    private static partial Regex XmlPattern();
}

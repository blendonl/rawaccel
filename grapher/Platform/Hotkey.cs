using System.Collections.Generic;
using Avalonia.Input;

namespace grapher.Platform;

public readonly record struct Hotkey(KeyModifiers Modifiers, Key Key)
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const KeyModifiers NonShiftModifiers = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta;

    public bool IsValid => VirtualKey != 0 && (Modifiers & NonShiftModifiers) != 0;

    public uint Win32Modifiers =>
        (Modifiers.HasFlag(KeyModifiers.Alt) ? ModAlt : 0) |
        (Modifiers.HasFlag(KeyModifiers.Control) ? ModControl : 0) |
        (Modifiers.HasFlag(KeyModifiers.Shift) ? ModShift : 0) |
        (Modifiers.HasFlag(KeyModifiers.Meta) ? ModWin : 0);

    public uint VirtualKey => Key switch
    {
        >= Key.A and <= Key.Z => 0x41 + (uint)(Key - Key.A),
        >= Key.D0 and <= Key.D9 => 0x30 + (uint)(Key - Key.D0),
        >= Key.F1 and <= Key.F24 => 0x70 + (uint)(Key - Key.F1),
        _ => 0,
    };

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', System.StringSplitOptions.TrimEntries);
        var modifiers = KeyModifiers.None;

        foreach (var part in parts[..^1])
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => KeyModifiers.Control,
                "alt" => KeyModifiers.Alt,
                "shift" => KeyModifiers.Shift,
                "win" => KeyModifiers.Meta,
                _ => (KeyModifiers?)null,
            };

            if (modifier is null)
            {
                return false;
            }

            modifiers |= modifier.Value;
        }

        if (!TryParseKey(parts[^1], out var key))
        {
            return false;
        }

        var parsed = new Hotkey(modifiers, key);

        if (!parsed.IsValid)
        {
            return false;
        }

        hotkey = parsed;
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    private static string KeyName(Key key) => key is >= Key.D0 and <= Key.D9
        ? ((char)('0' + (key - Key.D0))).ToString()
        : key.ToString();

    private static bool TryParseKey(string text, out Key key)
    {
        key = Key.None;

        if (text.Length == 1 && char.IsAsciiLetter(text[0]))
        {
            key = Key.A + (char.ToUpperInvariant(text[0]) - 'A');
        }
        else if (text.Length == 1 && char.IsAsciiDigit(text[0]))
        {
            key = Key.D0 + (text[0] - '0');
        }
        else if (text.Length > 1 && (text[0] is 'F' or 'f') && int.TryParse(text[1..], out int number) && number is >= 1 and <= 24)
        {
            key = Key.F1 + (number - 1);
        }

        return key != Key.None;
    }
}

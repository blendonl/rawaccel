using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace grapher.Platform;

public enum OverlayAction
{
    Lock = 1,
    Reset = 2,
    Close = 3,
}

public sealed record OverlayHotkeys(Hotkey Lock, Hotkey Reset, Hotkey Close)
{
    private const KeyModifiers CtrlAlt = KeyModifiers.Control | KeyModifiers.Alt;

    public static readonly OverlayHotkeys Default = new(new Hotkey(CtrlAlt, Key.R), new Hotkey(CtrlAlt, Key.E), new Hotkey(CtrlAlt, Key.Q));

    public static readonly OverlayAction[] Actions = { OverlayAction.Lock, OverlayAction.Reset, OverlayAction.Close };

    public Hotkey this[OverlayAction action] => action switch
    {
        OverlayAction.Lock => Lock,
        OverlayAction.Reset => Reset,
        _ => Close,
    };

    public bool IsDistinct => Actions.Select(a => this[a]).Distinct().Count() == Actions.Length;

    public static string Describe(OverlayAction action) => action switch
    {
        OverlayAction.Lock => "lock and unlock the speed overlay",
        OverlayAction.Reset => "reset the speed overlay stats",
        _ => "close the speed overlay",
    };

    public static OverlayHotkeys Parse(string? lockText, string? resetText, string? closeText)
    {
        var parsed = new OverlayHotkeys(
            Hotkey.TryParse(lockText, out var lockKey) ? lockKey : Default.Lock,
            Hotkey.TryParse(resetText, out var resetKey) ? resetKey : Default.Reset,
            Hotkey.TryParse(closeText, out var closeKey) ? closeKey : Default.Close);

        return parsed.IsDistinct ? parsed : Default;
    }

    public OverlayHotkeys With(OverlayAction action, Hotkey hotkey) => action switch
    {
        OverlayAction.Lock => this with { Lock = hotkey },
        OverlayAction.Reset => this with { Reset = hotkey },
        _ => this with { Close = hotkey },
    };

    public OverlayAction? UsedBy(Hotkey hotkey, OverlayAction except) =>
        Actions.Where(a => a != except && this[a] == hotkey).Cast<OverlayAction?>().FirstOrDefault();

    public IEnumerable<(OverlayAction Action, Hotkey Hotkey)> All => Actions.Select(a => (a, this[a]));
}

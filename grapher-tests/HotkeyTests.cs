using System.IO;
using Avalonia.Input;
using grapher.Platform;
using grapher.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class HotkeyTests
{
    [TestMethod]
    public void DefaultsAreCtrlAltRAndEAndQ()
    {
        var defaults = OverlayHotkeys.Default;

        Assert.AreEqual("Ctrl+Alt+R", defaults.Lock.ToString());
        Assert.AreEqual("Ctrl+Alt+E", defaults.Reset.ToString());
        Assert.AreEqual("Ctrl+Alt+Q", defaults.Close.ToString());
        Assert.AreEqual(0x0003u, defaults.Lock.Win32Modifiers);
        Assert.AreEqual(0x52u, defaults.Lock.VirtualKey);
        Assert.IsTrue(defaults.IsDistinct);
    }

    [TestMethod]
    public void FindsWhichActionAlreadyUsesAHotkey()
    {
        var defaults = OverlayHotkeys.Default;

        Assert.AreEqual(OverlayAction.Close, defaults.UsedBy(defaults.Close, OverlayAction.Lock));
        Assert.IsNull(defaults.UsedBy(defaults.Lock, OverlayAction.Lock));
        Assert.IsNull(defaults.UsedBy(new Hotkey(KeyModifiers.Alt, Key.F1), OverlayAction.Reset));

        var changed = defaults.With(OverlayAction.Reset, new Hotkey(KeyModifiers.Alt, Key.F1));
        Assert.AreEqual("Alt+F1", changed[OverlayAction.Reset].ToString());
        Assert.AreEqual(defaults.Lock, changed.Lock);
    }

    [TestMethod]
    [DataRow("Ctrl+Alt+R", KeyModifiers.Control | KeyModifiers.Alt, Key.R)]
    [DataRow("control + shift + f9", KeyModifiers.Control | KeyModifiers.Shift, Key.F9)]
    [DataRow("Win+5", KeyModifiers.Meta, Key.D5)]
    [DataRow("Alt+F24", KeyModifiers.Alt, Key.F24)]
    public void ParsesValidCombinations(string text, KeyModifiers modifiers, Key key)
    {
        Assert.IsTrue(Hotkey.TryParse(text, out var hotkey));
        Assert.AreEqual(new Hotkey(modifiers, key), hotkey);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("R")]
    [DataRow("Shift+R")]
    [DataRow("Ctrl+Space")]
    [DataRow("Ctrl+F25")]
    [DataRow("Hyper+R")]
    [DataRow("Ctrl+")]
    public void RejectsInvalidCombinations(string? text)
    {
        Assert.IsFalse(Hotkey.TryParse(text, out _));
    }

    [TestMethod]
    public void RoundTripsThroughText()
    {
        var hotkey = new Hotkey(KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta, Key.D0);

        Assert.AreEqual("Ctrl+Shift+Win+0", hotkey.ToString());
        Assert.IsTrue(Hotkey.TryParse(hotkey.ToString(), out var parsed));
        Assert.AreEqual(hotkey, parsed);
        Assert.AreEqual(0x30u, parsed.VirtualKey);
    }

    [TestMethod]
    public void ClickThroughStyleNeverTakesFocus()
    {
        const uint noActivate = 0x08000000;
        const uint transparent = 0x00000020;
        const uint layered = 0x00080000;

        uint locked = OverlayInterop.ExtendedStyle(0, clickThrough: true);
        Assert.AreEqual(noActivate | transparent | layered, locked);

        uint unlocked = OverlayInterop.ExtendedStyle(locked, clickThrough: false);
        Assert.AreEqual(noActivate, unlocked);
    }

    [TestMethod]
    public void SettingsFallBackToTheDefaultHotkey()
    {
        string path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, "{ \"SpeedOverlayLockHotkey\": \"Shift+R\", \"SpeedOverlayCloseHotkey\": \"Ctrl+Shift+F8\" }");
            var loaded = GuiSettings.Load(path);
            Assert.AreEqual("Ctrl+Alt+R", loaded.SpeedOverlayLockHotkey);
            Assert.AreEqual("Ctrl+Alt+E", loaded.SpeedOverlayResetHotkey);
            Assert.AreEqual("Ctrl+Shift+F8", loaded.SpeedOverlayCloseHotkey);

            File.WriteAllText(path, "{ \"SpeedOverlayLockHotkey\": \"Alt+F1\", \"SpeedOverlayResetHotkey\": \"Alt+F1\" }");
            Assert.AreEqual(OverlayHotkeys.Default, GuiSettings.Load(path).SpeedOverlayHotkeys);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

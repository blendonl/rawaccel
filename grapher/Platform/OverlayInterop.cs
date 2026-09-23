using System;
using System.Runtime.InteropServices;

namespace grapher.Platform;

public static partial class OverlayInterop
{
    public const uint WmHotkey = 0x0312;

    private const int GwlExStyle = -20;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint ModNoRepeat = 0x4000;
    private const uint LwaAlpha = 0x2;
    private const byte Opaque = 255;

    public static uint ExtendedStyle(uint exStyle, bool clickThrough)
    {
        exStyle |= WsExNoActivate;
        return clickThrough
            ? exStyle | WsExTransparent | WsExLayered
            : exStyle & ~(WsExTransparent | WsExLayered);
    }

    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        uint current = (uint)GetWindowLongPtrW(hwnd, GwlExStyle);
        SetWindowLongPtrW(hwnd, GwlExStyle, (IntPtr)ExtendedStyle(current, clickThrough));

        if (clickThrough)
        {
            SetLayeredWindowAttributes(hwnd, 0, Opaque, LwaAlpha);
        }
    }

    public static bool RegisterHotkey(IntPtr hwnd, int id, Hotkey hotkey) =>
        RegisterHotKey(hwnd, id, hotkey.Win32Modifiers | ModNoRepeat, hotkey.VirtualKey);

    public static void UnregisterHotkey(IntPtr hwnd, int id) => UnregisterHotKey(hwnd, id);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hwnd, int id);
}

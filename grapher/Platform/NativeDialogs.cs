using System;
using System.Runtime.InteropServices;

namespace grapher.Platform;

public static partial class NativeDialogs
{
    private const uint IconError = 0x10;

    public static void Show(string message, string caption) =>
        MessageBoxW(IntPtr.Zero, message, caption, IconError);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}

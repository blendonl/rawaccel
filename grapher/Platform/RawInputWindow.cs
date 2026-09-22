using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace grapher.Platform;

public readonly record struct RawMouseMove(IntPtr Device, int X, int Y);

public sealed partial class RawInputWindow : IDisposable
{
    private const uint WmInput = 0x00FF;
    private const uint WmInputDeviceChange = 0x00FE;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeMouse = 0;
    private const ushort MouseMoveAbsolute = 0x01;
    private const uint RideInputSink = 0x00000100;
    private const uint RideDevNotify = 0x00002000;
    private const ushort UsagePageGeneric = 0x01;
    private const ushort UsageMouse = 0x02;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly WndProc wndProc;
    private readonly string className = "RawAccelRawInput" + Guid.NewGuid().ToString("N");
    private readonly IntPtr instance;
    private IntPtr handle;

    public RawInputWindow()
    {
        wndProc = OnMessage;
        instance = GetModuleHandleW(null);

        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            Instance = instance,
            ClassName = className,
        };

        if (RegisterClassExW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        handle = CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, instance, IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var device = new RawInputDevice
        {
            UsagePage = UsagePageGeneric,
            Usage = UsageMouse,
            Flags = RideInputSink | RideDevNotify,
            Target = handle,
        };

        if (!RegisterRawInputDevices(ref device, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    public event Action<RawMouseMove>? MouseMoved;

    public event Action? DevicesChanged;

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            DestroyWindow(handle);
            handle = IntPtr.Zero;
            UnregisterClassW(className, instance);
        }
    }

    private IntPtr OnMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmInput)
        {
            ReadInput(lParam);
        }
        else if (message == WmInputDeviceChange)
        {
            DevicesChanged?.Invoke();
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private void ReadInput(IntPtr rawInput)
    {
        var data = new RawInputMouse();
        uint size = (uint)Marshal.SizeOf<RawInputMouse>();

        if (GetRawInputData(rawInput, RidInput, ref data, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue)
        {
            return;
        }

        if (data.Header.Type != RimTypeMouse || (data.Mouse.Flags & MouseMoveAbsolute) != 0)
        {
            return;
        }

        if (data.Mouse.LastX != 0 || data.Mouse.LastY != 0)
        {
            MouseMoved?.Invoke(new RawMouseMove(data.Header.Device, data.Mouse.LastX, data.Mouse.LastY));
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public ushort Reserved;
        public uint Buttons;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputMouse
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterRawInputDevices(ref RawInputDevice devices, uint count, uint size);

    [LibraryImport("user32.dll")]
    private static partial uint GetRawInputData(IntPtr rawInput, uint command, ref RawInputMouse data, ref uint size, uint headerSize);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandleW(string? moduleName);
}

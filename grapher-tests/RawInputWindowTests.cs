using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using grapher.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class RawInputWindowTests
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint PeekRemove = 0x0001;

    [TestMethod]
    [TestCategory("Interactive")]
    public void ReportsRelativeMouseMovement()
    {
        RawMouseMove? received = null;
        using var window = new RawInputWindow();
        window.MouseMoved += move => received ??= move;

        Move(3, 0);
        try
        {
            var watch = Stopwatch.StartNew();
            while (received is null && watch.ElapsedMilliseconds < 2000)
            {
                while (PeekMessageW(out var message, IntPtr.Zero, 0, 0, PeekRemove))
                {
                    TranslateMessage(ref message);
                    DispatchMessageW(ref message);
                }
            }
        }
        finally
        {
            Move(-3, 0);
        }

        if (received is null)
        {
            Assert.Inconclusive("No raw input arrived; the session may not have an interactive desktop.");
        }

        Assert.AreEqual(3, received.Value.X);
        Assert.AreEqual(0, received.Value.Y);
    }

    private static void Move(int dx, int dy)
    {
        var input = new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput { Dx = dx, Dy = dy, Flags = MouseEventMove },
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Hwnd;
        public uint Value;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out Message message, IntPtr hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref Message message);
}

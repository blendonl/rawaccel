using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace grapher_tests;

internal static class TestDevices
{
    public static MultiHandleDevice Connected(string id, string name, int handle)
    {
        var device = (MultiHandleDevice)RuntimeHelpers.GetUninitializedObject(typeof(MultiHandleDevice));
        device.id = id;
        device.name = name;
        device.handles = new List<IntPtr> { (IntPtr)handle };
        return device;
    }
}

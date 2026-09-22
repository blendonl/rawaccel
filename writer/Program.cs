using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace writer
{

    class Program
    {
        static readonly string DefaultPath = "settings.json";
        static readonly string Usage =
            $"Usage: {AppDomain.CurrentDomain.FriendlyName} <settings file path>\n";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        static void Exit(string msg)
        {
            MessageBoxW(IntPtr.Zero, msg, "Raw Accel writer", 0);
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            try
            {
                VersionHelper.ValidOrThrow();
            }
            catch (InteropException e)
            {
                Exit(e.Message);
            }

            try
            {
                if (args.Length != 1)
                {
                    if (File.Exists(DefaultPath))
                    {
                        Exit(Usage);
                    }
                    else
                    {
                        File.WriteAllText(DefaultPath, DriverConfig.GetDefault().ToJSON());
                        Exit($"{Usage}\n(generated default settings file '{DefaultPath}')");
                    }
                }
                else
                {
                    var result = DriverConfig.Convert(File.ReadAllText(args[0]));
                    if (result.Item2 == null)
                    {
                        result.Item1.Activate();
                    }
                    else
                    {
                        Exit($"Bad settings:\n\n{result.Item2}");
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Exit(e.Message);
            }
            catch (JsonException e)
            {
                Exit($"Settings format invalid:\n\n{e.Message}");
            }
            catch (Exception e)
            {
                Exit($"Error:\n\n{e}");
            }
        }
    }
}

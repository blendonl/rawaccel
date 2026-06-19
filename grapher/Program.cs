using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace grapher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            var mutex = new System.Threading.Mutex(true, "RawAccelGrapher", out bool result);

            if (!result)
            {
                MessageBox.Show("Another instance of the Raw Accel Grapher is already running.");
                return;
            }

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RawAcceleration(args.Any(a =>
                string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-background", StringComparison.OrdinalIgnoreCase))));

            GC.KeepAlive(mutex);      
        }

        static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            System.IO.File.WriteAllText("error.log", ex.ToString());
            MessageBox.Show(ex.Message, "Error");
        }
    }
}

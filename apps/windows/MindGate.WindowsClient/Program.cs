using System.Runtime.InteropServices;

namespace MindGate.WindowsClient;

static class Program
{
    private const string InstanceMutexName = @"Local\MindGate.WindowsClient";
    private const uint ShowApplicationMessage = 0x8000 + 42;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        using var instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            var existingWindow = FindWindow(null, "MindGate");
            if (existingWindow != IntPtr.Zero)
                PostMessage(existingWindow, ShowApplicationMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            var startInBackground = args.Any(argument =>
                string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));
            Application.Run(new DashboardForm(startInBackground));
        }
        finally
        {
            instanceMutex.ReleaseMutex();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}

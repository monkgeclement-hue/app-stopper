using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MindGate.WindowsClient;

internal sealed record ForegroundWindow(nint Handle, int ProcessId, string ExecutablePath);

internal static class ForegroundWindowReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        internal uint Size;
        internal uint Time;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

    internal static ForegroundWindow? Read()
    {
        var handle = GetForegroundWindow();
        if (handle == 0 || GetWindowThreadProcessId(handle, out var processId) == 0 || processId == 0)
            return null;

        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            var path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path))
                return null;
            return new ForegroundWindow(handle, checked((int)processId), Path.GetFullPath(path));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            // Some protected processes do not expose their executable path to this user.
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static bool IsForeground(nint handle) => handle != 0 && GetForegroundWindow() == handle;

    internal static bool TryGetIdleDuration(out TimeSpan idleDuration)
    {
        var lastInput = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref lastInput))
        {
            idleDuration = TimeSpan.Zero;
            return false;
        }

        var elapsedMilliseconds = unchecked((uint)Environment.TickCount - lastInput.Time);
        idleDuration = TimeSpan.FromMilliseconds(elapsedMilliseconds);
        return true;
    }
}

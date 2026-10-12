using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GmuLauncher.Services;

public static class WindowEx
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>شريط عنوان داكن (Windows 10 20H1+ / 11).</summary>
    public static void DarkTitle(Window w) =>
        w.SourceInitialized += (_, _) =>
        {
            try { int v = 1; DwmSetWindowAttribute(new WindowInteropHelper(w).Handle, 20, ref v, sizeof(int)); }
            catch { }
        };
}

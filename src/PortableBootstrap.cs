using System;
using System.IO;
using System.Runtime.InteropServices;

// Linked into both EXEs: startup must run before any native sound call.
internal static class PortableBootstrap
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint flags);

    public static void ShowError(string text)
    {
        MessageBox(IntPtr.Zero, "ZXMAK2 could not start.\n\n" + text +
            "\n\nCheck that this copy is writable and is not already running.", "ZXMAK2 portable startup", 0x10);
    }

    public static void Initialize()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var native = Path.Combine(root, "Lib", "Native");
        if (Directory.Exists(native) && !SetDllDirectory(native))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        // Do not change CWD: command-line user paths retain their meaning.
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NowcastPure
{
    public static class WindowTitles
    {
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);

        public static List<string> Read()
        {
            var titles = new List<string>();
            try
            {
                IntPtr hDesk = OpenInputDesktop(0, false, 0x0100 /* DESKTOP_READOBJECTS */);
                if (hDesk != IntPtr.Zero)
                {
                    EnumDesktopWindows(hDesk, (window, parameter) =>
                    {
                        var sb = new StringBuilder(512);
                        GetWindowText(window, sb, sb.Capacity);
                        string t = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(t))
                        {
                            titles.Add(t);
                        }
                        return true;
                    }, IntPtr.Zero);
                    CloseDesktop(hDesk);
                }
                else
                {
                    EnumWindows((window, parameter) =>
                    {
                        var sb = new StringBuilder(512);
                        GetWindowText(window, sb, sb.Capacity);
                        string t = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(t))
                        {
                            titles.Add(t);
                        }
                        return true;
                    }, IntPtr.Zero);
                }
            }
            catch { }
            return titles;
        }
    }
}

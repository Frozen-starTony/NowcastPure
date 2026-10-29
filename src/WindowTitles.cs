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
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);

        public static List<string> Read()
        {
            var titles = new List<string>();
            try
            {
                EnumWindows((window, parameter) =>
                {
                    if (!IsWindowVisible(window)) return true;
                    int length = GetWindowTextLength(window);
                    if (length <= 0) return true;
                    var sb = new StringBuilder(length + 1);
                    GetWindowText(window, sb, sb.Capacity);
                    string t = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        titles.Add(t);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            return titles;
        }
    }
}

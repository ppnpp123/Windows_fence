using System;
using System.Runtime.InteropServices;

namespace NoFences.Win32
{
    public class DesktopUtil
    {
        private const int GWL_STYLE = -16;
        private const int GWL_HWNDPARENT = -8;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLong64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        private static long GetWindowLongSafe(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLong64(hWnd, nIndex).ToInt64();
            else
                return GetWindowLong32(hWnd, nIndex);
        }

        private static void SetWindowLongSafe(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            if (IntPtr.Size == 8)
                SetWindowLong64(hWnd, nIndex, new IntPtr(dwNewLong));
            else
                SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
        }

        public static void PreventMinimize(IntPtr handle)
        {
            long style = GetWindowLongSafe(handle, GWL_STYLE);
            SetWindowLongSafe(handle, GWL_STYLE, style & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);
        }

        public static void GlueToDesktop(IntPtr handle)
        {
            IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            if (progman != IntPtr.Zero)
            {
                SetWindowLongSafe(handle, GWL_HWNDPARENT, progman.ToInt64());
            }
        }
    }
}

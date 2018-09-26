using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

using Move = System.Tuple<System.Tuple<int, int>, System.Tuple<int, int>>;

namespace HackMatcher {
    public class Util32 {
        static int PAUSE_MS = 20;
        public static IntPtr handle;
        public static InputSimulator sim = new InputSimulator();

        public static void ForegroundWindow() {
            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 0, 200, 1000, 400, true);
            SetForegroundWindow(Program.selfHandle);
            SetForegroundWindow(handle);
        }

        public static void ExecuteMoves(List<Move> moves) {
            foreach (Move move in moves) {
                int x1, y1;
                if (move.Item1.Item1 == -1) {
                    x1 = 1520;
                    y1 = 320;
                }
                else {
                    x1 = 393 + move.Item1.Item1 * 143 + 100;
                    y1 = 494 + move.Item1.Item2 * 32 + 16;
                }
                Point(x1, y1);
                Thread.Sleep(PAUSE_MS);
                sim.Mouse.LeftButtonDown();
                Thread.Sleep(PAUSE_MS);
                int x2, y2;
                if (move.Item2.Item1 == -1) {
                    x2 = 1520;
                    y2 = 320;
                }
                else {
                    x2 = 393 + move.Item2.Item1 * 143 + 100;
                    y2 = 494 + move.Item2.Item2 * 32 + 16;
                }
                Point(x2, y2);
                Thread.Sleep(PAUSE_MS);
                sim.Mouse.LeftButtonUp();
                Thread.Sleep(PAUSE_MS);
            }
        }
        public static void ClickNewGame() {
            Point(1460, 960);
            Thread.Sleep(PAUSE_MS);
            sim.Mouse.LeftButtonDown();
            Thread.Sleep(PAUSE_MS);
            sim.Mouse.LeftButtonUp();
            Thread.Sleep(PAUSE_MS);
        }

        public static void Point(int x, int y) {
            Point point = new Point(x, y);
            ClientToScreen(handle, ref point);
            Rectangle screen_bounds = Screen.GetBounds(point);
            int px = (int)(point.X * 65535 / screen_bounds.Width);
            int py = (int)(point.Y * 65535 / screen_bounds.Height);
            mouse_event(0x00000001 | 0x00008000, px, py, 0, 0);
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hwnd, ref Point lpPoint);
        [DllImport("User32.Dll")]
        public static extern long SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [Flags]
        public enum MouseEventFlags : int {
            LEFTDOWN = 0x00000002,
            LEFTUP = 0x00000004,
            MIDDLEDOWN = 0x00000020,
            MIDDLEUP = 0x00000040,
            MOVE = 0x00000001,
            ABSOLUTE = 0x00008000,
            RIGHTDOWN = 0x00000008,
            RIGHTUP = 0x00000010
        }
    }
}
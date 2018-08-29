using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace HackMatcher {
    public class Util32 {
        static int PAUSE_MS = 18;
        public static IntPtr handle;
        public static InputSimulator sim = new InputSimulator();

        public static void ForegroundWindow() {
            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 300, 200, 1000, 400, true);
            SetForegroundWindow(Program.selfHandle);
            SetForegroundWindow(handle);
        }

        public static void ExecuteMoves(Queue<Move> moves) {
            int grabberCol = 0;
            while (moves.Count > 0) {
                Move move = moves.Dequeue();
                while (move.col < grabberCol) {
                    sim.Keyboard.KeyDown(VirtualKeyCode.VK_A);
                    grabberCol--;
                    Thread.Sleep(PAUSE_MS);
                    sim.Keyboard.KeyUp(VirtualKeyCode.VK_A);
                    Thread.Sleep(PAUSE_MS);
                }
                while (move.col > grabberCol) {
                    sim.Keyboard.KeyDown(VirtualKeyCode.VK_D);
                    grabberCol++;
                    Thread.Sleep(PAUSE_MS);
                    sim.Keyboard.KeyUp(VirtualKeyCode.VK_D);
                    Thread.Sleep(PAUSE_MS);
                }
                VirtualKeyCode keyCode = move.operation == Operation.GRAB_OR_DROP ? VirtualKeyCode.VK_J : VirtualKeyCode.VK_K;
                sim.Keyboard.KeyDown(keyCode);
                Thread.Sleep(PAUSE_MS);
                sim.Keyboard.KeyUp(keyCode);
                Thread.Sleep(PAUSE_MS);
            }
            for (int i = 0; i < 6; i++) {
                sim.Keyboard.KeyDown(VirtualKeyCode.VK_A);
                Thread.Sleep(PAUSE_MS);
                sim.Keyboard.KeyUp(VirtualKeyCode.VK_A);
                Thread.Sleep(PAUSE_MS);
            }
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    }
}
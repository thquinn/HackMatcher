using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Move = System.Tuple<System.Tuple<int, int>, System.Tuple<int, int>>;

namespace HackMatcher {
    class Program {
        public static IntPtr selfHandle, hWnd;

        static void Main(string[] args) {
            // Run EXAPUNKS at 1366*768 resolution and disable HACK*MATCH CRT effect in the settings.
            // Launch HACK*MATCH, wait for the menu to show, then launch the solver.

            Process[] processes = Process.GetProcessesByName("EXAPUNKS");
            if (processes.Length == 0) {
                Console.WriteLine("Couldn't find an open instance of EXAPUNKS. Press any key to quit.");
                Console.ReadKey();
                return;
            }
            Process process = processes.OrderBy(e => e.StartTime).First();
            selfHandle = Process.GetCurrentProcess().MainWindowHandle;
            hWnd = process.MainWindowHandle;
            if (hWnd != IntPtr.Zero) {
                Util32.handle = hWnd;
            }
            Util32.ForegroundWindow();

            while (true) {
                CaptureAndSolve();
                Thread.Sleep(2000);
                Util32.ClickNewGame();
                Thread.Sleep(5500);
            }
        }

        static void CaptureAndSolve() {
            Image image = ScreenCapture.CaptureWindow(hWnd);
            Bitmap bitmap = new Bitmap(1200, 120, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap)) {
                Rectangle srcRect = new Rectangle(393, 494, 1200, 120);
                Rectangle destRect = new Rectangle(0, 0, 1200, 120);
                g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
            }
            bitmap.Save("last.png");
            Piece[,] pieces = CV.ReadBitmap(bitmap);
            if (pieces == null) {
                return;
            }
            State start = new State(pieces);
            PriorityQueue<int, State> queue = new PriorityQueue<int, State>();
            queue.Enqueue(0, start);
            Dictionary<State, Tuple<State, Move>> parents = new Dictionary<State, Tuple<State, Move>>();
            State finalState = null;

            while (true) {
                if (queue.Count > 100000) {
                    return;
                }
                State state = queue.Dequeue().Value;
                Dictionary<Move, State> children = state.GetChildren();
                bool done = false;
                foreach (KeyValuePair<Move, State> child in children) {
                    if (parents.ContainsKey(child.Value)) {
                        continue;
                    }
                    parents[child.Value] = new Tuple<State, Move>(state, child.Key);
                    if (child.Value.IsSolved()) {
                        finalState = child.Value;
                        done = true;
                        break;
                    }
                    queue.Enqueue(child.Value.Eval(), child.Value);
                }
                if (done) {
                    break;
                }
            }
            if (finalState == null) {
                Console.WriteLine("No solution found!");
            }

            List<Move> moves = new List<Move>();
            while (true) {
                if (!parents.ContainsKey(finalState)) {
                    break;
                }
                moves.Add(parents[finalState].Item2);
                finalState = parents[finalState].Item1;
            }
            moves.Reverse();
            Console.WriteLine(string.Join("\n", moves));
            Util32.ExecuteMoves(moves);
        }
    }

    class State {
        Piece[,] board;
        Piece free;

        public State(Piece[,] board) {
            this.board = board;
        }
        public State(State other) {
            board = new Piece[other.board.GetLength(0), other.board.GetLength(1)];
            for (int x = 0; x < board.GetLength(0); x++) {
                for (int y = 0; y < board.GetLength(1); y++) {
                    if (other.board[x, y] != null) {
                        board[x, y] = new Piece() { suit = other.board[x, y].suit, value = other.board[x, y].value };
                    }
                }
            }
            if (other.free != null) {
                free = new Piece() { suit = other.free.suit, value = other.free.value };
            }
        }

        public Dictionary<Move, State> GetChildren() {
            Dictionary<Move, State> children = new Dictionary<Move, State>();
            // Find grabbable stacks.
            for (int x = 0; x < board.GetLength(0); x++) {
                for (int y = board.GetLength(1) - 1; y >= 0; y--) {
                    if (board[x, y] == null) {
                        continue;
                    }
                    if (y < board.GetLength(1) - 1 && board[x, y + 1] != null) {
                        if (!board[x, y + 1].CanStackOn(board[x, y])) {
                            break;
                        }
                    }
                    if (y == 3 && board[x, y].value == 0) {
                        if (board[x, 0].suit == board[x, 3].suit && board[x, 1].suit == board[x, 3].suit && board[x, 2].suit == board[x, 3].suit) {
                            break;
                        }
                    }
                    // Find places to put this stack.
                    for (int x2 = 0; x2 < board.GetLength(0); x2++) {
                        if (x2 == x) {
                            continue;
                        }
                        int y2 = board.GetLength(1) - 1;
                        while (y2 >= 0 && board[x2, y2] == null) {
                            y2--;
                        }
                        y2++;
                        if (y2 > 0) {
                            if (!board[x, y].CanStackOn(board[x2, y2 - 1])) {
                                continue;
                            }
                        }
                        Move move = new Move(new Tuple<int, int>(x, y), new Tuple<int, int>(x2, y2));
                        State child = GetChild(move);
                        if (child != null) {
                            children.Add(move, GetChild(move));
                        }
                    }
                }
            }
            // Freecell.
            for (int x = 0; x < board.GetLength(0); x++) {
                int y = board.GetLength(1) - 1;
                while (y >= 0 && board[x, y] == null) {
                    y--;
                }
                if (y >= 0 && free == null) {
                    Move move = new Move(new Tuple<int, int>(x, y), new Tuple<int, int>(-1, -1));
                    children.Add(move, GetChild(move));
                }
                if (free != null && (y == -1 || free.CanStackOn(board[x, y]))) {
                    if (y < 0) {
                        y = 0;
                    }
                    Move move = new Move(new Tuple<int, int>(-1, -1), new Tuple<int, int>(x, y));
                    State child = GetChild(move);
                    if (child != null) {
                        children.Add(move, child);
                    }
                }
            }
            return children;
        }
        private State GetChild(Move move) {
            State child = new State(this);
            if (move.Item1.Item1 == -1) { // From freecell.
                child.board[move.Item2.Item1, move.Item2.Item2] = free;
                child.free = null;
            }
            else if (move.Item2.Item1 == -1) { // To freecell.
                child.free = child.board[move.Item1.Item1, move.Item1.Item2];
                child.board[move.Item1.Item1, move.Item1.Item2] = null;
            }
            else {
                for (int dy = 0; move.Item1.Item2 + dy < board.GetLength(1) && board[move.Item1.Item1, move.Item1.Item2 + dy] != null; dy++) {
                    child.board[move.Item2.Item1, move.Item2.Item2 + dy] = board[move.Item1.Item1, move.Item1.Item2 + dy];
                    child.board[move.Item1.Item1, move.Item1.Item2 + dy] = null;
                }
            }
            // Check for block of 4 locked face cards not directly on the board.
            if (move.Item2.Item1 >= 0 && child.board[move.Item2.Item1, move.Item2.Item2].value == 0 && move.Item2.Item2 >= 4) {
                if (child.board[move.Item2.Item1, move.Item2.Item2].suit == child.board[move.Item2.Item1, move.Item2.Item2 - 1].suit &&
                    child.board[move.Item2.Item1, move.Item2.Item2].suit == child.board[move.Item2.Item1, move.Item2.Item2 - 2].suit &&
                    child.board[move.Item2.Item1, move.Item2.Item2].suit == child.board[move.Item2.Item1, move.Item2.Item2 - 3].suit) {
                    return null;
                }
            }
            return child;
        }

        public bool IsSolved() {
            if (free != null) {
                return false;
            }
            for (int x = 0; x < board.GetLength(0); x++) {
                if (board[x, 0] == null) {
                    continue;
                }
                if (board[x, 0].value != 0 && board[x, 0].value != 10)
                    return false;
                for (int y = 1; y < board.GetLength(1); y++) {
                    if (board[x, y] == null) {
                        break;
                    }
                    if (!board[x, y].CanStackOn(board[x, y - 1])) {
                        return false;
                    }
                }
            }
            return true;
        }
        public int Eval() {
            int total = 0;
            for (int x = 0; x < board.GetLength(0); x++) {
                for (int y = 1; y < board.GetLength(1); y++) {
                    if (board[x, y] != null && board[x, y].CanStackOn(board[x, y - 1])) {
                        total--;
                    }
                }
            }
            return total;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < board.GetLength(1); y++) {
                for (int x = 0; x < board.GetLength(0); x++) {
                    Piece piece = board[x, y];
                    sb.Append(piece == null ? "  " : piece.ToString());
                    sb.Append(' ');
                }
                sb.Append('\n');
            }
            if (free != null) {
                sb.Append(free.ToString());
            }
            return sb.ToString();
        }
        public override bool Equals(object obj) {
            if (obj.GetType() != typeof(State))
                return false;
            State other = (State)obj;
            if ((free == null) != (other.free == null)) {
                return false;
            }
            if (free != null) {
                if (free.suit != other.free.suit || free.value != other.free.value) {
                    return false;
                }
            }

            for (int x = 0; x < board.GetLength(0); x++) {
                for (int y = 0; y < board.GetLength(1); y++) {
                    Piece piece = board[x, y];
                    Piece otherPiece = other.board[x, y];
                    if ((piece == null) != (otherPiece == null)) {
                        return false;
                    }
                    if (piece != null) {
                        if (piece.suit != otherPiece.suit || piece.value != otherPiece.value) {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        public override int GetHashCode() {
            return ToString().GetHashCode();
        }
    }
}
